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
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.PortForward
{
    /// <summary>
    /// Implements <see cref="IReversePortForwardConnector"/>
    /// </summary>
    internal class ReversePortForwardConnector : IReversePortForwardConnector
    {
        private readonly ILog _log;
        private readonly int _port;
        private readonly ConcurrentDictionary<int, StreamHookData> _streams = new ConcurrentDictionary<int, StreamHookData>();
        private TcpListener _listener;
        private readonly Lazy<IPlatform> _platform;
        private const int _SocketBufferSize = 40960; // 40KB

        public ReversePortForwardConnector(int port, ILog log, Lazy<IPlatform> platform)
        {
            _port = port;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _log.Verbose($"ReversePortForwardConnector created for port {port}");
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// <see cref="IReversePortForwardConnector.ConnectAsync(Func{int, Task}, Func{int, byte[], int, Task}, Func{int, Task})"/>
        /// </summary>
        public Task<bool> ConnectAsync(Func<int, Task> connectHandler, Func<int, byte[], int, Task> receiveHandler, Func<int, Task> closeHandler)
        {
            if (_listener != null)
            {
                _listener.Stop();
            }
            _listener = new TcpListener(IPAddress.Any, _port);
            try
            {
                _listener.Start();
                _log.Verbose($"ReversePortForwardConnector start listening on port {_port}");
            }
            catch (Exception ex)
            {
                _log.Verbose($"ReversePortForwardConnector start listening on port {_port} failed with {ex.Message}");
                return Task.FromResult(false);
            }
            Task.Run(async () =>
            {
                while (true)
                {
                    TcpClient tcpClient = null;
                    try
                    {
                        tcpClient = await _listener.AcceptTcpClientAsync();
                    }
                    catch (Exception ex)
                    {
                        _log.Verbose($"ReversePortForwardConnector AcceptTcpClientAsync on port {_port} failed with {ex.Message}.");
                        _listener.Stop();
                        _listener = null;
                        return;
                    }

                    var id = Interlocked.Increment(ref StreamHookData._streamIdCtr);
                    _log.Verbose($"ReversePortForwardConnector on port {_port} accepted incoming request as stream {id}.");
                    var stream = new StreamHookData(tcpClient, id, connectHandler, receiveHandler, closeHandler);
                    _streams[id] = stream;
                    Task.Run(() => this.StartReceiveDataAsync(stream)).Forget();
                }
            }).Forget();
            return Task.FromResult(true);
        }

        /// <summary>
        /// <see cref="IReversePortForwardConnector.SendAsync(int, byte[], CancellationToken)"/>
        /// </summary>
        public async Task<bool> SendAsync(int id, byte[] data, CancellationToken cancellationToken)
        {
            StreamHookData s;
            if (_streams.TryGetValue(id, out s) && s != null)
            {
                try
                {
                    _log.Verbose($"Send {data.Length} via {id}");
                    await s.TcpClient.GetStream().WriteAsync(data, 0, data.Length, cancellationToken);
                    return true;
                }
                catch (Exception ex)
                {
                    // will return false to the caller
                    _log.Error($"PortForwardConnector.SendAsync {id} exception {ex}");
                    this.Disconnect(s.Id);
                }
            }
            return false;
        }

        /// <summary>
        /// <see cref="IReversePortForwardConnector.Stop"/>
        /// </summary>
        public void Stop()
        {
            _listener.Stop();
            _listener = null;
            foreach (var id in _streams.Keys.ToArray())
            {
                this.Disconnect(id);
            }
            _streams.Clear();
        }

        /// <summary>
        /// <see cref="IReversePortForwardConnector.Disconnect(int)"/>
        /// </summary>
        public void Disconnect(int id)
        {
            StreamHookData s;
            if (_streams.TryRemove(id, out s) && s != null)
            {
                _log.Verbose($"PortForwardConnector.Disconnect {id}");
                s.Stop();
            }
        }

        /// <summary>
        /// <see cref="IReversePortForwardConnector.IsProbeRequest(byte[], int, string)"/>
        /// </summary>
        /// <remarks>The request is a valid probe request if it matches with the probe endpoint and the request actually came from Kubernetes api-server
        /// How do we check where the request generated from?
        /// Case1: If the request IP is 192.168.x.x (Docker desktop for K8s) or if nslookup on the request IP
        /// is able to get resolved - it means the request came from either outside the cluster or from another microservice inside the cluster.
        /// Case2: If it is unable to get resolved - it means the request probably came from the kube api-server and is a probe request</remarks>
        public bool IsProbeRequest(int id, byte[] buffer, int length, string probe)
        {
            if (!IsProbeEndpoint(buffer, length, probe))
            {
                return false;
            }

            StreamHookData s;
            if (_streams.TryGetValue(id, out s) && ((IPEndPoint)s?.TcpClient?.Client?.RemoteEndPoint)?.Address == null)
            {
                return true;
            }
            const string nslookupUnableToFindString = "server can't find";
            var requestIp = ((IPEndPoint)s.TcpClient.Client.RemoteEndPoint).Address.ToString();
            if (requestIp.StartsWith("192.168"))
            {
                return false;
            }

            var stdOutSb = new StringBuilder();
            var stdErrSb = new StringBuilder();
            var result = _platform.Value.Execute(
                executable: "nslookup",
                command: requestIp,
                stdOutCallback: (outString) => stdOutSb.AppendLine(outString),
                stdErrCallback: (errorString) => stdErrSb.AppendLine(errorString),
                envVariables: null,
                timeout: TimeSpan.FromSeconds(2),
                cancellationToken: CancellationToken.None,
                out string output,
                out string error);

            if (!string.IsNullOrWhiteSpace(error))
            {
                _log.Error("IsProbeRequest: nslookup error : {0}. Assuming this to be a probe request.", error);
                return true;
            }

            _log.Verbose("IsProbeRequest: nslookup output : " + output);

            // nslookup failure call -> request came from kube-api server and is a probe request
            return output.Contains(nslookupUnableToFindString);
        }

        /// <summary>
        /// <see cref="IReversePortForwardConnector.AnswerWithHttpOkAsync(int)"/>
        /// </summary>
        public async Task AnswerWithHttpOkAsync(int id)
        {
            string responseText = "HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
            var responseBytes = Encoding.ASCII.GetBytes(responseText);
            _log.Info("Answer with HTTP Okay async.");
            await this.SendAsync(id, responseBytes, CancellationToken.None);
        }

        #region private members

        private async Task StartReceiveDataAsync(StreamHookData s)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(_SocketBufferSize);
            try
            {
                NetworkStream stream = s.TcpClient.GetStream();
                await s.ConnectHandler(s.Id);
                int cRead = 0;
                do
                {
                    cRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    _log.Verbose($"ReversePortForwardConnector.HookupStreamData ReadAsync {s.Id} returns {cRead} bytes.");
                    if (cRead > 0 && s.DataHandler != null)
                    {
                        await s.DataHandler.Invoke(s.Id, buffer, cRead);
                    }
                } while (cRead > 0);
                _log.Verbose($"ReversePortForwardConnector.StartReceiveDataAsync finishes stream {s.Id}.");
            }
            catch (Exception ex)
            {
                _log.Verbose($"ReversePortForwardConnector.StartReceiveDataAsync exception '{ex.Message}' when invoking handler. Close.");
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                this.Disconnect(s.Id);
            }
        }

        /// <summary>
        /// Does the request endpoint match with the probe request on the pod spec
        /// </summary>
        private bool IsProbeEndpoint(byte[] buffer, int length, string probe)
        {
            if (string.IsNullOrEmpty(probe))
            {
                return false;
            }
            string expectedHTTPRequest = $"GET {probe} HTTP/1.1";
            var expectedBytes = Encoding.ASCII.GetBytes(expectedHTTPRequest);
            return expectedBytes.Length <= length && expectedBytes.SequenceEqual(buffer.Take(expectedBytes.Length));
        }

        #endregion private members
    }
}