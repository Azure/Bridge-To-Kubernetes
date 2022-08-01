// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Manages services and other objects on the remote environment
    /// </summary>
    internal interface IRemoteEnvironmentManager
    {
        /// <summary>
        /// Start the RemoteAgent in the cluster. If the POD is already running, the container image will be patched. Otherwise a
        /// new POD will be started with only devhostAgent image.
        /// </summary>
        Task<RemoteAgentInfo> StartRemoteAgentAsync(
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken);

        /// <summary>
        /// Starts a Kubernetes port forwart to the RemoteAgent
        /// </summary>
        /// <returns>The local port where the Remote Agent has been forwarded</returns>
        Task<int> ConnectToRemoteAgentAsync(
            RemoteAgentInfo remoteAgentInfo,
            CancellationToken cancellationToken);

        /// <summary>
        /// Wait for the connected container to terminate.
        /// </summary>
        /// <remarks>This is used to figure out if something happened to our remote agent and to restart it if necessary.</remarks>
        Task<bool> WaitForWorkloadStoppedAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Restore remote container/pod to its initial state.
        /// </summary>
        Task RestoreWorkloadAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Returns the port to which the RemoteAgent has been port forwarded.
        /// </summary>
        /// <returns></returns>
        int GetRemoteAgentLocalPort();
    }
}