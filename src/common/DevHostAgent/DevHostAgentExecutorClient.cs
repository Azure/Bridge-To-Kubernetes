// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Features.OwnedInstances;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.BridgeToKubernetes.Common.DevHostAgent
{
    internal class DevHostAgentExecutorClient : IDevHostAgentExecutorClient
    {
        private HubConnection _connection;
        private SemaphoreSlim _syncObject = new SemaphoreSlim(1);
        private bool _disposed = false;
        private readonly ILog _log;
        private List<CancellationTokenRegistration> _reversePortForwardStartCancellationTokenRegistrations = new List<CancellationTokenRegistration>();
        private List<CancellationTokenRegistration> _servicePortForwardStartCancellationTokenRegistrations = new List<CancellationTokenRegistration>();

        /// <summary>
        /// Factory delegate for <see cref="DevHostAgentExecutorClient"/>
        /// </summary>
        internal delegate IDevHostAgentExecutorClient Factory(int localPort);

        /// <summary>
        /// Owned factory delegate for <see cref="DevHostAgentExecutorClient"/>
        /// </summary>
        internal delegate Owned<IDevHostAgentExecutorClient> OwnedFactory(int localPort);

        public DevHostAgentExecutorClient(int localPort, ILog log)
        {
            LocalPort = localPort;
            _log = log;
        }

        public int LocalPort { get; }

        public async Task<bool> PingAsync(int timeoutMs, int retry, CancellationToken cancellationToken)
        {
            for (int i = 0; i < retry; i++)
            {
                this._CheckDisposed();
                try
                {
                    var connection = await this._GetConnectionAsync(cancellationToken);
                    await connection.InvokeAsync("Ping", cancellationToken);
                    return true;
                }
                catch (Exception ex)
                {
                    _log.Exception(ex);
                }
                await Task.Delay(timeoutMs);
            }
            return false;
        }

        /// <summary>
        /// Send a reset signal to devhostAgent. devhostAgent should enter a clean state.
        /// </summary>
        public async Task ResetAsync(CancellationToken cancellationToken)
        {
            try
            {
                var connection = await this._GetConnectionAsync(cancellationToken);
                await connection.InvokeAsync<bool>("Reset", cancellationToken);
                _log.Info($"Reset completed");
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }

        #region Reverse port forwarding

        public async Task ReversePortForwardStartAsync(PortForwardStartInfo port, Func<int, byte[], Task> dataHandler, Action<int> closedHandler, CancellationToken cancellationToken)
        {
            var connection = await this._GetConnectionAsync(cancellationToken);
            var channelReader = await connection.StreamAsChannelAsync<PortForwardStreamBlock>("RunReversePortForward", port, cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                PortForwardStreamBlock b = null;

                try
                {
                    b = await channelReader.ReadAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    // Cancellation requested
                    break;
                }

                switch (b.Flag)
                {
                    case PortForwardStreamFlag.Connected:
                        _reversePortForwardStartCancellationTokenRegistrations.Add(cancellationToken.Register(async () =>
                        {
                            try
                            {
                                await connection.InvokeAsync("StopReversePortForward", port.Port, b.StreamId);
                            }
                            catch (TaskCanceledException)
                            {
                                // Task Canceled
                            }
                            catch (Exception ex)
                            {
                                // This is needed because this run inside a task that is forgotten, so any exception that is not processed inside the task is going to get leaked outside the normal processing scope.
                                // We have a bug to change this pattern here https://devdiv.visualstudio.com/DevDiv/_boards/board/t/Mindaro/Stories/?workitem=1178734
                                _log.Exception(ex);
                            }
                        }));
                        break;

                    case PortForwardStreamFlag.Data:
                        try
                        {
                            await dataHandler(b.StreamId, b.Content);
                        }
                        catch (Exception dex)
                        {
                            // closes this connection
                            try
                            {
                                await connection.InvokeAsync("StopReversePortForward", port.Port, b.StreamId);
                            }
                            catch (Exception ex)
                            {
                                _log.Exception(ex);
                            }

                            _log.Exception(dex);

                            closedHandler(b.StreamId);
                        }
                        break;

                    case PortForwardStreamFlag.Closed:
                        closedHandler(b.StreamId);
                        break;

                    default:
                        throw new InvalidOperationException("Invalid protocol - expect flag 2 (data) or 3 (closed).");
                }
            }
            return;
        }

        public async Task ReversePortForwardSendAsync(int port, int streamId, byte[] content, CancellationToken cancellationToken)
        {
            var connection = await this._GetConnectionAsync(cancellationToken);
            try
            {
                if (!await connection.InvokeAsync<bool>("SendReversePortForwardData", port, streamId, content, cancellationToken))
                {
                    throw new InvalidOperationException($"ReversePortForward: Failed to send data on port: {port}");
                }
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }

        public async Task ReversePortForwardStopAsync(int port, int streamId, CancellationToken cancellationToken)
        {
            var connection = await this._GetConnectionAsync(cancellationToken);
            try
            {
                await connection.InvokeAsync("StopReversePortForward", port, streamId, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }

        #endregion Reverse port forwarding

        #region Service port forwarding

        public async Task<int> ServicePortForwardStartAsync(string serviceDns, int port, Func<byte[], Task> dataHandler, Action closedHandler, CancellationToken cancellationToken = default(CancellationToken))
        {
            int streamId = -1;
            var connection = await this._GetConnectionAsync(cancellationToken);

            var channelReader = await connection.StreamAsChannelAsync<PortForwardStreamBlock>("RunServicePortForward", serviceDns, port, cancellationToken);
            PortForwardStreamBlock block = await channelReader.ReadAsync(cancellationToken);
            switch (block.Flag)
            {
                case PortForwardStreamFlag.Connected:
                    streamId = block.StreamId;
                    _servicePortForwardStartCancellationTokenRegistrations.Add(cancellationToken.Register(async () =>
                    {
                        try
                        {
                            await connection.InvokeAsync("StopServicePortForward", serviceDns, port, streamId);
                        }
                        catch (Exception ex)
                        {
                            _log.Exception(ex);
                        }
                    }));
                    Task.Run(async () =>
                    {
                        bool continueRead = true;
                        while (continueRead)
                        {
                            var b = await channelReader.ReadAsync(cancellationToken);
                            switch (b.Flag)
                            {
                                case PortForwardStreamFlag.Data:
                                    await dataHandler(b.Content);
                                    break;

                                case PortForwardStreamFlag.Closed:
                                    continueRead = false;
                                    closedHandler();
                                    break;

                                default:
                                    throw new InvalidOperationException("Invalid protocol - expect flag 2 (data) or 3 (closed).");
                            }
                        }
                    }).Forget();
                    break;

                case PortForwardStreamFlag.Closed:
                    break;

                default:
                    throw new InvalidOperationException("Invalid protocol - expect flag 1 (connected) or 3 (closed).");
            }
            return streamId;
        }

        public async Task ServicePortForwardSendAsync(string serviceDns, int port, int streamId, byte[] content, CancellationToken cancellationToken)
        {
            var connection = await this._GetConnectionAsync(cancellationToken);
            try
            {
                if (!await connection.InvokeAsync<bool>("SendServicePortForwardData", serviceDns, port, streamId, content, cancellationToken))
                {
                    throw new InvalidOperationException($"ServicePortForward: Failed to send data to {serviceDns}:{port}");
                }
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }

        public async Task ServicePortForwardStopAsync(string serviceDns, int port, int streamId, CancellationToken cancellationToken)
        {
            var connection = await this._GetConnectionAsync(cancellationToken);
            try
            {
                await connection.InvokeAsync("StopServicePortForward", serviceDns, port, streamId, cancellationToken);
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
            }
        }

        #endregion Service port forwarding

        public void Dispose()
        {
            var c = _connection;
            if (c != null)
            {
                _connection = null;
                try
                {
                    c.DisposeAsync().AsTask().Wait(100);
                }
                catch (Exception)
                {
                    // ignore disconnect exceptions
                }
            }
            _reversePortForwardStartCancellationTokenRegistrations.ExecuteForEach(reg => reg.Dispose());
            _servicePortForwardStartCancellationTokenRegistrations.ExecuteForEach(reg => reg.Dispose());
            _disposed = true;
        }

        #region private members

        private void _CheckDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(typeof(DevHostAgentExecutorClient).Name);
            }
        }

        private async Task<HubConnection> _GetConnectionAsync(CancellationToken cancellationToken)
        {
            await _syncObject.WaitAsync();
            try
            {
                if (_connection == null)
                {
                    _connection = await this._ConnectAsync(cancellationToken);
                    _connection.Closed += (e) =>
                    {
                        _connection = null;
                        return Task.CompletedTask;
                    };
                }
                if (_connection == null)
                {
                    throw new InvalidOperationException("agent disconnected.");
                }
                return _connection;
            }
            finally
            {
                _syncObject.Release();
            }
        }

        private async Task<HubConnection> _ConnectAsync(CancellationToken cancellationToken)
        {
            var connection = new HubConnectionBuilder()
                .WithUrl($"http://{IPAddress.Loopback}:{LocalPort}/api/signalr/agent",
                opt =>
                {
                    opt.WebSocketConfiguration = c =>
                    {
                        c.KeepAliveInterval = TimeSpan.FromSeconds(3);
                    };
                }).AddMessagePackProtocol()
                .Build();
            await connection.StartAsync(cancellationToken);
            return connection;
        }

        #endregion private members
    }
}