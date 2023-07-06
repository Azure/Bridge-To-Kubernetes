// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.BridgeToKubernetes.DevHostAgent.PortForward;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.Services
{
    /// <summary>
    /// AgentExecutorHub implements a SignalR hub that exposes the following:
    ///  - execution control
    ///  - file synchronization
    /// </summary>
    internal class AgentExecutorHub : Hub
    {
        private readonly ILog _log;
        private readonly Lazy<IPlatform> _platform;
        private static ConcurrentDictionary<string, ServicePortForwardConnector> _servicePortForwardConnectors = new ConcurrentDictionary<string, ServicePortForwardConnector>();
        private static ConcurrentDictionary<int, ReversePortForwardConnector> _reversePortForwardConnectors = new ConcurrentDictionary<int, ReversePortForwardConnector>();

        public AgentExecutorHub(
            Lazy<IPlatform> platform,
            ILog log)
        {
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        private static long _numConnectedSessions;

        /// <summary>
        /// Gets the number of clients connected to this Hub
        /// </summary>
        public static int NumConnectedSessions => (int)Interlocked.Read(ref _numConnectedSessions);

        /// <summary>
        /// Called when clients connect
        /// </summary>
        public override Task OnConnectedAsync()
        {
            Interlocked.Increment(ref _numConnectedSessions);
            return base.OnConnectedAsync();
        }

        /// <summary>
        /// Called when clients disconnect
        /// </summary>
        public override Task OnDisconnectedAsync(Exception exception)
        {
            Interlocked.Decrement(ref _numConnectedSessions);
            return base.OnDisconnectedAsync(exception);
        }

        #region execution control

        /// <summary>
        /// Return the devhostAgent to initial state.
        /// </summary>
        [HubMethodName("Reset")]
        public bool Reset()
        {
            foreach (var c in _servicePortForwardConnectors.Values.ToArray())
            {
                try { c.Stop(); } catch (Exception ex) { _log.Info($"Exception stop servicePortForwardConnectors: {ex}"); }
            }
            _servicePortForwardConnectors.Clear();
            foreach (var c in _reversePortForwardConnectors.Values.ToArray())
            {
                try { c.Stop(); } catch (Exception ex) { _log.Info($"Exception stop reversePortForwardConnectors: {ex}"); }
            }
            _reversePortForwardConnectors.Clear();
            return true;
        }

        /// <summary>
        /// Handles ping
        /// </summary>
        [HubMethodName("Ping")]
        public void Ping()
        {
            return;
        }

        #endregion execution control

        #region Connect

        #region Reverse port forwarding

        [HubMethodName("RunReversePortForward")]
        public ChannelReader<PortForwardStreamBlock> RunReversePortForward(PortForwardStartInfo startInfo)
        {
            var channel = Channel.CreateBounded<PortForwardStreamBlock>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            ReversePortForwardConnector connector = null;
            Task.Run(async () =>
            {
                connector = _reversePortForwardConnectors.GetOrAdd(startInfo.Port, (p) => new ReversePortForwardConnector(p, _log, _platform));
                await connector.ConnectAsync(
                async (id) =>
                {
                    _log.Verbose($"AgentHub connnected for {startInfo.Port}, id {id}");
                    await channel.Writer.WriteAsync(PortForwardStreamBlock.Connected(id));
                },
                async (id, buffer, length) =>
                {
                    _log.Verbose($"AgentHub received for {startInfo.Port}, id {id}, size {length}");
                    foreach (var probe in startInfo.HttpProbes)
                    {
                        if (connector.IsProbeRequest(id, buffer, length, probe))
                        {
                            _log.Verbose($"Probe request on stream {id}");
                            await connector.AnswerWithHttpOkAsync(id);
                            return;
                        }
                    }
                    await channel.Writer.WriteAsync(PortForwardStreamBlock.Data(id, buffer, length));
                },
                async (id) =>
                {
                    _log.Verbose($"AgentHub closed for {startInfo.Port}, id {id}");
                    await channel.Writer.WriteAsync(PortForwardStreamBlock.Closed(id));
                });
            }).Forget();
            return channel.Reader;
        }

        [HubMethodName("SendReversePortForwardData")]
        public async Task<bool> SendReversePortForwardDataAsync(int port, int streamId, byte[] content)
        {
            ReversePortForwardConnector connector;
            if (!_reversePortForwardConnectors.TryGetValue(port, out connector))
            {
                return false;
            }
            _log.Verbose($"AgentHub Send for {port}, id {streamId} {content.Length} bytes");
            return await connector.SendAsync(streamId, content, this.Context.ConnectionAborted);
        }

        [HubMethodName("StopReversePortForward")]
        public void StopReversePortForward(int port, int streamId)
        {
            ReversePortForwardConnector connector;
            if (_reversePortForwardConnectors.TryGetValue(port, out connector))
            {
                _log.Verbose($"AgentHub disconnect for {port}, id {streamId}");
                connector.Disconnect(streamId);
            }
        }

        #endregion Reverse port forwarding

        #region Service port forwarding

        [HubMethodName("RunServicePortForward")]
        public ChannelReader<PortForwardStreamBlock> RunServicePortForward(string serviceDns, int port)
        {
            var channel = Channel.CreateBounded<PortForwardStreamBlock>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            string key = this.GetServicePortForwardKey(serviceDns, port);
            var connector = _servicePortForwardConnectors.GetOrAdd(key, (_) => new ServicePortForwardConnector(serviceDns, port, _log));

            _log.Verbose($"RunServicePortForward: connector created for {key}.");

            Task.Run(async () =>
            {
                await connector.ConnectAsync(
                    async (id) =>
                    {
                        _log.Verbose($"ServicePortForward connected for {serviceDns}:{port}, id {id}");
                        await channel.Writer.WriteAsync(PortForwardStreamBlock.Connected(id));
                    },
                    async (id, buffer, length) =>
                    {
                        _log.Verbose($"ServicePortForward received {length} bytes from {serviceDns}:{port}, id {id}.");
                        await channel.Writer.WriteAsync(PortForwardStreamBlock.Data(id, buffer, length));
                    },
                    async (id) =>
                    {
                        _log.Verbose($"ServicePortForward {serviceDns}:{port}, id {id} closed.");
                        await channel.Writer.WriteAsync(PortForwardStreamBlock.Closed(id));
                    });
            }).Forget();
            return channel.Reader;
        }

        [HubMethodName("SendServicePortForwardData")]
        public async Task<bool> SendServicePortForwardDataAsync(string serviceDns, int port, int streamId, byte[] content)
        {
            ServicePortForwardConnector connector;
            if (!_servicePortForwardConnectors.TryGetValue(this.GetServicePortForwardKey(serviceDns, port), out connector))
            {
                return false;
            }
            else
            {
                _log.Verbose($"ServicePortForward send {content.Length} bytes to stream {streamId}");
                return await connector.SendAsync(streamId, content, this.Context.ConnectionAborted);
            }
        }

        [HubMethodName("StopServicePortForward")]
        public void StopServicePortForward(string serviceDns, int port, int streamId)
        {
            ServicePortForwardConnector connector;
            if (_servicePortForwardConnectors.TryGetValue(this.GetServicePortForwardKey(serviceDns, port), out connector))
            {
                _log.Verbose($"ServicePortForward disconnected for {port}, id {streamId}");
                connector.Disconnect(streamId);
            }
        }

        private string GetServicePortForwardKey(string serviceDns, int port)
        {
            return $"{serviceDns}:{port}";
        }

        #endregion Service port forwarding

        #endregion Connect
    }
}