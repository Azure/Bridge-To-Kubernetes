// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Features.OwnedInstances;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    internal class ServicePortForwardManager : IServicePortForwardManager, IDisposable
    {
        private readonly Owned<IDevHostAgentExecutorClient> _agentClient;
        private readonly ILog _log;

        private readonly CancellationTokenRegistration _cancellationRegistration;
        private CancellationTokenSource _cancellationTokenSource;
        private List<ServicePortForwardInstance> _servicePortForwardInstances = new List<ServicePortForwardInstance>();

        public delegate IServicePortForwardManager Factory(Owned<IDevHostAgentExecutorClient> agentClient, CancellationToken cancellationToken);

        public ServicePortForwardManager(
            Owned<IDevHostAgentExecutorClient> agentClient,
            CancellationToken cancellationToken,
            ILog log)
        {
            this._agentClient = agentClient;
            this._cancellationRegistration = cancellationToken.Register(() => this._agentClient.Dispose());
            this._cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this._log = log;
        }

        /// <summary>
        /// <see cref="IServicePortForwardManager.Start(ServicePortForwardStartInfo)"/>
        /// </summary>
        public void Start(ServicePortForwardStartInfo startInfo)
        {
            var instance = new ServicePortForwardInstance(startInfo, this._agentClient.Value, this._log);
            _servicePortForwardInstances.Add(instance);
            instance.RunAsync(this._cancellationTokenSource.Token).Forget();
        }

        /// <summary>
        /// <see cref="IServicePortForwardManager.Stop"/>
        /// </summary>
        public void Stop()
        {
            try
            {
                this._agentClient?.Dispose();
                this._cancellationRegistration.Dispose();
                if (this._cancellationTokenSource != null)
                {
                    var tempCts = this._cancellationTokenSource;
                    this._cancellationTokenSource = null;
                    tempCts.Cancel();
                    tempCts.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // Cancellation token disposed in between cancellation
            }
        }

        /// <summary>
        /// Dispose
        /// </summary>
        public void Dispose()
        {
            _cancellationRegistration.Dispose();
            _cancellationTokenSource?.Dispose();
            _servicePortForwardInstances?.ForEach(instance => instance.Dispose());
        }

        /// <summary>
        /// Private implementation class
        /// </summary>
        private class ServicePortForwardInstance : IDisposable
        {
            private readonly IDevHostAgentExecutorClient _agentClient;
            private readonly string _serviceDns;
            private readonly int _servicePort;
            private readonly int _localPort;
            private readonly TcpListener _tcpListener;
            private readonly ILog _log;
            private const int BUFFER_SIZE = 40960;
            private CancellationTokenSource _tcpListenerCancellationTokenSource;
            private CancellationTokenRegistration _tcpListenerCancellationTokenRegistration = default(CancellationTokenRegistration);
            private CancellationTokenRegistration _tcpListenerStoppingCancellationRegistration = default(CancellationTokenRegistration);

            public ServicePortForwardInstance(ServicePortForwardStartInfo startInfo, IDevHostAgentExecutorClient agentClient, ILog log)
            {
                _agentClient = agentClient;
                _log = log;
                _serviceDns = startInfo.ServiceDns;
                // If the _serviceDns is empty DevHostAgent ouputs the traffic to localhost.
                if (StringComparer.OrdinalIgnoreCase.Equals(_serviceDns, Constants.DAPR))
                {
                    _serviceDns = string.Empty;
                }

                _servicePort = startInfo.ServicePort;
                _localPort = startInfo.LocalPort ?? startInfo.ServicePort;
                var ip = startInfo.IP;
                if (ip == null)
                {
                    ip = IPAddress.Any;
                }
                _tcpListener = new TcpListener(ip, _localPort);
                _tcpListenerCancellationTokenSource = new CancellationTokenSource();
                _tcpListenerCancellationTokenRegistration = _tcpListenerCancellationTokenSource.Token.Register(() => { try { _tcpListener.Stop(); } catch (Exception) { } });
                _tcpListener.Start();
            }

            public async Task RunAsync(CancellationToken cancellationToken)
            {
                await Task.Yield();
                _log.Verbose($"ServicePortForwarder started on {_serviceDns}:{_servicePort} on local port {_localPort}");
                _tcpListenerStoppingCancellationRegistration = cancellationToken.Register(() => _tcpListenerCancellationTokenSource.Cancel());
                while (_tcpListener != null)
                {
                    var localConnection = await _tcpListener.AcceptTcpClientAsync();
                    if (_tcpListener == null)
                    {
                        break;
                    }
                    _log.Verbose($"Accept {_localPort}");
                    this.RunLoopAsync(localConnection, cancellationToken).Forget();
                }
            }

            private async Task RunLoopAsync(TcpClient connection, CancellationToken cancellationToken)
            {
                await Task.Yield();

                using (var requestProcessingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                using (requestProcessingCancellationTokenSource.Token.Register(() => this.StopLocal(connection)))
                {
                    try
                    {
                        var stream = connection.GetStream();
                        int streamId = 0;
                        requestProcessingCancellationTokenSource.Token.ThrowIfCancellationRequested();
                        streamId = await _agentClient.ServicePortForwardStartAsync(_serviceDns, _servicePort,
                            async (data) =>
                            {
                                _log.Verbose($"ServicePortForward: received {data.Length} bytes in stream {streamId}.");
                                // data handler
                                await stream.WriteAsync(data, 0, data.Length, requestProcessingCancellationTokenSource.Token);
                            },
                            () =>
                            {
                                _log.Verbose($"ServicePortForward: stream {streamId} closed.");
                                // closed handler
                                requestProcessingCancellationTokenSource.Cancel();
                            },
                            requestProcessingCancellationTokenSource.Token);
                        _log.Verbose($"ServicePortForward: stream {streamId} connected.");

                        byte[] buffer = new byte[BUFFER_SIZE];

                        while (!requestProcessingCancellationTokenSource.Token.IsCancellationRequested)
                        {
                            int cRead = 0;
                            try
                            {
                                cRead = await stream.ReadAsync(buffer, requestProcessingCancellationTokenSource.Token);
                            }
                            catch (IOException ex)
                            {
                                if (ex.InnerException is OperationCanceledException)
                                {
                                    // Cancellation requested
                                    break;
                                }
                                if (ex.InnerException is SocketException se && (se.SocketErrorCode == SocketError.ConnectionReset || se.SocketErrorCode == SocketError.OperationAborted))
                                {
                                    _log.Verbose($"Connection is already closed by DevHostAgent on socket {streamId} (RunLoopAsync)");
                                    requestProcessingCancellationTokenSource.Cancel();
                                    break;
                                }

                                throw;
                            }

                            if (cRead == 0)
                            {
                                _log.Verbose($"ServicePortForward: stream {streamId}: StopLocal");
                                requestProcessingCancellationTokenSource.Cancel();
                                await this.StopRemoteAsync(streamId);
                                break;
                            }
                            else
                            {
                                _log.Verbose($"ServicePortForward: sending {cRead} bytes via stream {streamId}");
                                byte[] content = new byte[cRead];
                                Array.Copy(buffer, content, cRead);
                                try
                                {
                                    await _agentClient.ServicePortForwardSendAsync(_serviceDns, _servicePort, streamId, content, requestProcessingCancellationTokenSource.Token);
                                }
                                catch (Exception ex)
                                {
                                    _log.Verbose($"ServicePortForward: stream {streamId} send failed with {ex}.");
                                    await this.StopRemoteAsync(streamId);
                                }
                            }
                        }
                    }
                    // Ignore this type of exception since it arises when we try to use a socket that is closed which may happen at the time of cancellation
                    // System.IO.IOException: Unable to read data from the transport connection: The I/O operation has been aborted because of either a thread exit or an application request..
                    // ---> System.Net.Sockets.SocketException (995): The I/O operation has been aborted because of either a thread exit or an application request
                    // Bug https://devdiv.visualstudio.com/DevDiv/_workitems/edit/1172442
                    catch (IOException ex) when (ex.InnerException.GetType() == typeof(SocketException)) { _log.ExceptionAsWarning(ex); }
                    catch (Exception ex)
                    {
                        _log.Verbose($"ServicePortForwarder.RunLoopAsync exception: {ex}");
                    }
                }
            }

            private void StopLocal(TcpClient connection)
            {
                try
                {
                    try
                    {
                        connection?.Close();
                    }
                    catch (Exception e) { _log.ExceptionAsWarning(e); }
                    finally
                    {
                        connection?.Dispose();
                    }
                }
                catch (ObjectDisposedException) { }
            }

            private async Task StopRemoteAsync(int streamId)
            {
                await _agentClient.ServicePortForwardStopAsync(_serviceDns, _servicePort, streamId, CancellationToken.None);
            }

            public void Dispose()
            {
                try
                {
                    _tcpListenerCancellationTokenSource?.Cancel();
                    _tcpListenerCancellationTokenRegistration.Dispose();
                    _tcpListenerStoppingCancellationRegistration.Dispose();
                    _tcpListenerCancellationTokenSource?.Dispose();
                }
                catch (ObjectDisposedException) { }
            }
        }
    }
}