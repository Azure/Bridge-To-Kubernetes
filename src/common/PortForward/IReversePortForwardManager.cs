// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models.Channel;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    /// <summary>
    /// <see cref="IReversePortForwardManager"/> talks to the remote devhostAgent via SignalR. <see cref="IReversePortForwardManager"/> forwards
    /// the port in the devhostAgent container to local.
    /// </summary>
    internal interface IReversePortForwardManager
    {
        /// <summary>
        /// Starts reverse port forward
        /// </summary>
        void Start(PortForwardStartInfo port);

        /// <summary>
        /// Stops all reverse port forward connections started by this instance
        /// </summary>
        void Stop();
    }
}