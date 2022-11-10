// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.PortForward
{
    /// <summary>
    /// Implements <see cref="IServicePortForwardConnector"/>
    /// </summary>
    internal class ServicePortForwardConnector : IServicePortForwardConnector
    {
        private readonly ILog _log;
        private readonly int _port;
        private readonly string _targetService;
        private readonly ConcurrentDictionary<int, StreamHookData> _streams = new ConcurrentDictionary<int, StreamHookData>();
        private readonly string ImdsEndpoint = "169.254.169.254";
        private const int _SocketBufferSize = 40960; // 40KB
        private string loggingPrefix = "";
        private bool isManagedIdentity = false;

        /// <summary>
        /// Creates an instance that forwards another service's port.
        /// </summary>
        public ServicePortForwardConnector(string targetService, int port, ILog log)
        {
            _port = port;
            _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (StringComparer.OrdinalIgnoreCase.Equals(_targetService, Common.Constants.ManagedIdentity.TargetServiceNameOnLocalMachine))
            {
                isManagedIdentity = true;
                loggingPrefix = "ManagedIdentity : ";
            }
        }

        /// <summary>
        /// <see cref="IServicePortForwardConnector.ConnectAsync(Func{int, Task}, Func{int, byte[], int, Task}, Func{int, Task})"/>
        /// </summary>
        public async Task<int> ConnectAsync(Func<int, Task> connectHandler, Func<int, byte[], int, Task> receiveHandler, Func<int, Task> closeHandler)
        {
            TcpClient tcpClient = null;
            try
            {
                if (string.IsNullOrEmpty(_targetService))
                {
                    tcpClient = new TcpClient();
                    await tcpClient.ConnectAsync(IPAddress.Loopback, _port);
                }
                else if (isManagedIdentity)
                {
                    tcpClient = new TcpClient(ImdsEndpoint, 80);
                }
                else
                {
                    tcpClient = new TcpClient(_targetService, _port);
                }

                var id = Interlocked.Increment(ref StreamHookData._streamIdCtr);
                var stream = new StreamHookData(tcpClient, id, connectHandler, receiveHandler, closeHandler);
                _streams[id] = stream;
                this.HookupStreamData(stream).Forget();
                return id;
            }
            catch (Exception e)
            {
                _log.Exception(e);
                tcpClient?.Dispose();
                return -1;
            }
        }

        /// <summary>
        /// <see cref="IServicePortForwardConnector.SendAsync(int, byte[], CancellationToken)"/>
        /// </summary>
        public async Task<bool> SendAsync(int id, byte[] data, CancellationToken cancellationToken)
        {
            StreamHookData s;
            if (_streams.TryGetValue(id, out s) && s != null)
            {
                try
                {
                    _log.Verbose($"{loggingPrefix}Send {data.Length} via {id}");

                    if (isManagedIdentity)
                    {
                        data = GetModifiedBytesForManagedIdentity(data);
                    }

                    await s.TcpClient.GetStream().WriteAsync(data, 0, data.Length, cancellationToken);
                    return true;
                }
                catch (Exception ex)
                {
                    // will return false to the caller
                    _log.Error($"{loggingPrefix}PortForwardConnector.SendAsync {id} exception {ex}");
                }
            }
            return false;
        }

        /// <summary>
        /// <see cref="IServicePortForwardConnector.Stop"/>
        /// </summary>
        public void Stop()
        {
            foreach (var id in _streams.Keys.ToArray())
            {
                this.Disconnect(id);
            }
            _streams.Clear();
        }

        /// <summary>
        /// <see cref="IServicePortForwardConnector.Disconnect(int)"/>
        /// </summary>
        public void Disconnect(int id)
        {
            StreamHookData s;
            if (_streams.TryRemove(id, out s) && s != null)
            {
                _log.Verbose($"{loggingPrefix}PortForwardConnector.Disconnect {id}");
                s.Stop();
            }
        }

        #region private members

        private async Task HookupStreamData(StreamHookData s)
        {
            await Task.Yield();
            var buffer = ArrayPool<byte>.Shared.Rent(_SocketBufferSize);
            try
            {
                await s.ConnectHandler(s.Id);
                NetworkStream stream = s.TcpClient.GetStream();
                int cRead = 0;
                do
                {
                    cRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    _log.Verbose($"{loggingPrefix}PortForwardConnector.HookupStreamData ReadAsync {s.Id} returns {cRead} bytes.");

                    if (cRead > 0 && s.DataHandler != null)
                    {
                        await s.DataHandler.Invoke(s.Id, buffer, cRead);
                    }
                } while (cRead > 0);
                this.Disconnect(s.Id);
            }
            catch (Exception ex)
            {
                _log.Verbose($"{loggingPrefix}PortForwardConnector.HookupStreamData {s.Id} caught exception {ex.Message}.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// This method receives the managed identity request in a certain format, and we modify the request so that it becomes a valid
        /// managed identity request that we then send to the IMDS endpoint (169.254.169.254)
        /// This is a temporary hack (using MSI_ENDPOINT and MSI_SECRET) till the Identity team updates their nuget package to send the
        /// right request when the right environment variable is set.
        /// We replace the apiversion param and the clientid to client_id. We also add Metadata:true header to the request
        /// </summary>
        /// <param name="data"></param>
        /// <remarks>
        /// input data should look like below:
        /// GET /metadata/identity/oauth2/token?api-version=2017-09-01&mi_res_id=%2Fsubscriptions%2F4be8920b-2978-43d7-ab14-04d8549c1d05%2FresourceGroups%2FAKSE2EInfra%2Fproviders%2FMicrosoft.ManagedIdentity%2FuserAssignedIdentities%2Fakse2ehcp&resource=https%3A%2F%2Fstorage.azure.com%2F
        /// GET /metadata/identity/oauth2/token?api-version=2017-09-01&resource=https%3A%2F%2Fstorage.azure.com&clientid=<guid> HTTP/1.1
        /// \nHost: managedidentity
        /// \nsecret: placeholder
        /// \nx-ms-client-request-id: e6aadc2a-bb98-4714-bc4c-719676e0d4cf
        /// \nx-ms-return-client-request-id: true
        /// \nUser-Agent: azsdk-net-Identity/1.4.0-alpha.20210223.1 (.NET Core 3.1.12; Microsoft Windows 10.0.19042)
        ///
        /// we would modify it to something like below:
        /// GET /metadata/identity/oauth2/token?api-version=2018-02-01&resource=https%3A%2F%2Fstorage.azure.com&client_id=<guid> HTTP/1.1
        /// \nHost: managedidentity
        /// \nsecret: placeholder
        /// \nMetadata: true
        /// \nx-ms-client-request-id: 8569c7c4-2411-400f-9d92-5b2e78ec7ec6
        /// \nx-ms-return-client-request-id: true
        /// \nUser-Agent: azsdk-net-Identity/1.3.0 (.NET Core 3.1.12; Microsoft Windows 10.0.19042)
        /// </remarks>
        private byte[] GetModifiedBytesForManagedIdentity(byte[] data)
        {
            const string headerToInsert = "Metadata: true";
            var stringContent = Encoding.UTF8.GetString(data);
            // This log may not work as expected depending on the character on the string, adding print line inside 
            // the for lop below to avoid wrong information while debugging
            _log.Verbose($"{loggingPrefix} Original data : {stringContent}");
            stringContent = stringContent.Replace("2017-09-01", "2018-02-01").Replace("clientid", "client_id");
            var lines = stringContent.Split(new char[] { '\n' }).ToList();
            int index = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                _log.Verbose($"{loggingPrefix} Printing line : {lines[i]}");
                if (lines[i].Contains($"secret: {Common.Constants.ManagedIdentity.SecretValue}", StringComparison.OrdinalIgnoreCase))
                {
                    index = i;
                    break;
                }
            }
            if (index != -1) {
                lines.Insert(index + 1, headerToInsert);
            }
            else {
                lines.Append(headerToInsert);
            }
            var modifiedRequestString = string.Join('\n', lines);

            _log.Verbose($"{loggingPrefix} Sending data : {modifiedRequestString}");

            return Encoding.UTF8.GetBytes(modifiedRequestString);
        }

        #endregion private members
    }
}