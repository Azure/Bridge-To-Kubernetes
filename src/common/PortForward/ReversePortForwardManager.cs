// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Concurrent;
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
    internal class ReversePortForwardManager : IReversePortForwardManager, IDisposable
    {
        private readonly Owned<IDevHostAgentExecutorClient> _agentClient;
        private readonly ILog _log;

        private readonly CancellationTokenRegistration _cancellationRegistration;
        private CancellationTokenSource _cancellationTokenSource;

        public delegate IReversePortForwardManager Factory(Owned<IDevHostAgentExecutorClient> agentClient, CancellationToken cancellationToken);

        public ReversePortForwardManager(Owned<IDevHostAgentExecutorClient> agentClient, CancellationToken cancellationToken, ILog log)
        {
            this._agentClient = agentClient;
            this._cancellationRegistration = cancellationToken.Register(() => this._agentClient.Dispose());
            this._cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            this._log = log;
        }

        /// <summary>
        /// <see cref="IReversePortForwardManager.Start(PortForwardStartInfo)"/>
        /// </summary>
        public void Start(PortForwardStartInfo port)
        {
            var instance = new ReversePortForwardInstance(port, this._agentClient.Value, this._log);
            instance.Start(this._cancellationTokenSource.Token);
        }

        /// <summary>
        /// <see cref="IReversePortForwardManager.Stop"/>
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
            this._cancellationRegistration.Dispose();
            this._cancellationTokenSource?.Dispose();
        }

        /// <summary>
        /// Private implementation class
        /// </summary>
        private class ReversePortForwardInstance
        {
            private PortForwardStartInfo _port;
            private IDevHostAgentExecutorClient _agentClient;
            private ILog _log;
            private int _localPort;
            private ConcurrentDictionary<int, TcpClient> _streams;
            private const int BUFFER_SIZE = 81920;
            private CancellationToken _cancellationToken;

            public ReversePortForwardInstance(PortForwardStartInfo port, IDevHostAgentExecutorClient agentClient, ILog log)
            {
                _port = port;
                _localPort = _port.LocalPort ?? _port.Port;
                _agentClient = agentClient;
                _log = log;
                _streams = new ConcurrentDictionary<int, TcpClient>();
            }

            public void Start(CancellationToken cancellationToken)
            {
                _cancellationToken = cancellationToken;
                Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            await _agentClient.ReversePortForwardStartAsync(_port, this.OnDataReceived, this.OnClosed, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _log.Verbose($"ReversePortForwardStartAsync failed with {ex}");
                        }
                        await Task.Delay(1000);
                    }
                }).Forget();
            }

            private async Task OnDataReceived(int streamId, byte[] data)
            {
                var tcpClient = _streams.GetOrAdd(streamId, (_) =>
                {
                    TcpClient t = new TcpClient();
                    t.Connect(new IPEndPoint(IPAddress.Loopback, _localPort));
                    Task.Run(() => this.StartReceivingAsync(t, streamId, _cancellationToken)).Forget();
                    return t;
                });

                if (tcpClient.Connected)
                {
                    await tcpClient.GetStream().WriteAsync(data, 0, data.Length);
                    _log.Verbose($"Sent {data.Length} bytes to workload on id {streamId}.");
                }
            }

            private void OnClosed(int streamId)
            {
                TcpClient tcpClient;
                if (_streams.TryRemove(streamId, out tcpClient) && tcpClient != null)
                {
                    _log.Verbose($"Closing socket {streamId}");
                    tcpClient.Close();
                    tcpClient.Dispose();
                }
            }

            private async Task StartReceivingAsync(TcpClient tcpClient, int streamId, CancellationToken cancellation)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(BUFFER_SIZE);
                try
                {
                    while (!cancellation.IsCancellationRequested)
                    {
                        int cRead = 0;
                        try
                        {
                            cRead = await tcpClient.GetStream().ReadAsync(buffer, cancellation);
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
                                _log.Verbose($"Connection is already closed by DevHostAgent on socket {streamId} (StartReceivingAsync)");
                                break;
                            }

                            throw;
                        }

                        _log.Verbose($"ReversePortForwarder receive {cRead} bytes from port {_port.Port} on id {streamId}");

                        if (cRead == 0)
                        {
                            await _agentClient.ReversePortForwardStopAsync(_port.Port, streamId, cancellation);
                            break;
                        }

                        byte[] content = new byte[cRead];
                        Array.Copy(buffer, content, cRead);

                        try
                        {
                            await _agentClient.ReversePortForwardSendAsync(_port.Port, streamId, content, cancellation);
                        }
                        catch (Exception ex)
                        {
                            _log.Verbose($"ReversePortForwarder exception '{ex.Message}' transferring {cRead} bytes from workload port {_port.Port} on id {streamId}");
                            this.OnClosed(streamId);
                            throw;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Verbose($"StartReceivingAsync {_port.Port} id {streamId} exception {ex}");
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}