// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models.Settings;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    internal class KubernetesPortForwardManager : IKubernetesPortForwardManager
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly IStreamManager _streamManager;
        private readonly ILog _log;
        private readonly PortListener.Factory _portListenerFactory;

        private const string webSocketSubProtocol = "v4.channel.k8s.io";

        public delegate IKubernetesPortForwardManager Factory(IKubernetesClient kubernetesClient);

        public KubernetesPortForwardManager(
            IKubernetesClient kubernetesClient,
            IStreamManager streamManager,
            ILog log,
            PortListener.Factory portListenerFactory)
        {
            this._kubernetesClient = kubernetesClient;
            this._portListenerFactory = portListenerFactory;
            this._streamManager = streamManager;
            this._log = log;
        }

        /// <summary>
        /// <see cref="IKubernetesPortForwardManager.StartContainerPortForward"/>
        /// </summary>
        public void StartContainerPortForward(
            string namespaceName,
            string podName,
            int localPort,
            int remotePort,
            Action<PortPair> onSuccessfulPortForward = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            Task.Run(async () =>
            {
                using (var portListener = _portListenerFactory(localPort, cancellationToken))
                {
                    try
                    {
                        _log.Verbose("Starting listening {0} : {1}", localPort, remotePort);
                        // Define how to get a webSocket on the pod Remote port
                        Func<Task<WebSocket>> createWebSocketAsync = async () =>
                        {
                            _log.Verbose("Creating web socket for {0} {1}", new PII(podName), remotePort);
                            Exception exception = null;
                            WebSocket ws = null;
                            for (int retry = 0; retry < 3; retry++)
                            {
                                try
                                {
                                    ws = await this._kubernetesClient.WebSocketPodPortForwardAsync(
                                        namespaceName,
                                        podName,
                                        new int[] { remotePort },
                                        webSocketSubProtocol);
                                    _log.Verbose("Web socket for {0} {1} created.", new PII(podName), remotePort);
                                    return ws;
                                }
                                catch (WebSocketException ex)
                                {
                                    exception = ex;
                                    await Task.Delay(100);
                                }
                            }
                            _log.Verbose("Creating web socket for {0} {1} failed with {2}", new PII(podName), remotePort, exception.Message);
                            throw exception;
                        };

                        onSuccessfulPortForward?.Invoke(new PortPair(localPort, remotePort)); // This should probably not be invoked here. Since we just defined the function and haven't invoked it yet.

                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var localConnection = await portListener.Listener.AcceptTcpClientAsync();
                            _log.Verbose("Accept {0} to {1}", localPort, remotePort);

                            _streamManager.Start(
                                localConnection: localConnection,
                                remoteConnectionFactory: createWebSocketAsync,
                                remotePort: remotePort,
                                logMessagePrefixFormat: "Port forward {0} {1}:{2} {3}",
                                logMessagePrefixArgs: new object[] { new PII(podName), localPort, remotePort, ((IPEndPoint)localConnection.Client.RemoteEndPoint).Port },
                                log: _log,
                                cancellationToken: cancellationToken);
                        }
                    }
                    catch (ObjectDisposedException)
                    {
                        // Port forwarding has been canceled (e.g. Ctrl+C)
                    }
                    catch (Exception e)
                    {
                        _log.Exception(e);
                        _log.Error($"Port forwarding {namespaceName}/{podName} {localPort}:{remotePort} failed with exception : {e.Message}");
                        throw;
                    }
                }
            }).Forget();
        }
    }
}