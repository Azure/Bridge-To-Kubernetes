// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models.Channel;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    /// <summary>
    /// <see cref="IServicePortForwardManager"/> talks to the remote devhostAgent via SignalR. <see cref="IServicePortForwardManager"/> forwards the
    /// remote service-address:port from devhostAgent to local machine.
    /// </summary>
    internal interface IServicePortForwardManager
    {
        /// <summary>
        /// Starts a service port forward
        /// </summary>
        void Start(ServicePortForwardStartInfo startInfo);

        /// <summary>
        /// Stops all running port forwarding instances
        /// </summary>
        void Stop();
    }
}