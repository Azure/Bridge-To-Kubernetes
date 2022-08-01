// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    public interface IConnectManagementClient : IDisposable
    {
        #region GetInformation

        /// <summary>
        /// Collects the environment of the user's container running in the cluster
        /// </summary>
        /// <returns></returns>
        /// <remarks>This is the first method of the Connect workflow that needs to be called as the environment should be pulled before deploying the remote agent.</remarks>
        Task<WorkloadInfo> GetWorkloadInfo();

        /// <summary>
        /// Gets which elevated permissions the client will need to proceed with Connect operations
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IEnumerable<IElevationRequest>> GetElevationRequestsAsync(CancellationToken cancellationToken);

        #endregion GetInformation

        #region RemoteAgent

        /// <summary>
        /// Starts the remote agent in place of the original remote container, returns the RemoteAgent inforamtion (i.e. Namespace, pod, container)
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns>The <see cref="RemoteAgentInfo"/> relative to the container running the remote agent</returns>
        Task<RemoteAgentInfo> StartRemoteAgentAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Connects to the deployed RemoteAgent
        /// </summary>
        /// <param name="remoteAgentInfo"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The local port where the Remore Agent was port forwarded</returns>
        Task<int> ConnectToRemoteAgentAsync(RemoteAgentInfo remoteAgentInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Waits for a change in the remote agent (stop/replaced). This can be used to restart the connection if a change happens.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        // TODO: (lolodi) we should use event or at least take a callback
        Task WaitRemoteAgentChangeAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Restores the original remote container.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RestoreOriginalRemoteContainerAsync(CancellationToken cancellationToken);

        #endregion RemoteAgent

        #region LocalMachine

        /// <summary>
        /// Maps local ports and IPs to the reachable remote services.
        /// </summary>
        Task AddLocalMappingsAsync(int[] localPorts, IEnumerable<IElevationRequest> elevationRequests, CancellationToken cancellationToken);

        /// <summary>
        /// Configure and starts the LocalAgent container that is going to serve as sidecar to the user's containerized workload.
        /// </summary>
        /// <remarks>This is used when the user's workload runs containerized</remarks>
        Task<string> StartLocalAgentAsync(int[] localPorts, KubeConfigDetails kubeConfigDetails, RemoteAgentInfo remoteAgentInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Starts the ServicePortForwards and the ReversePortForwards to and from the cluster
        /// </summary>
        Task StartServicePortForwardingsAsync(int remoteAgentLocalPort, IEnumerable<EndpointInfo> reachableEndpoints, IEnumerable<PortForwardStartInfo> reversePortForwardInfo, CancellationToken cancellationToken);

        /// <summary>
        /// Mount volumes and lists all the environment variables to be passed to the user's workload.
        /// </summary>
        /// <param name="localPorts">The local port(s) where the service running locally is listening to.</param>
        /// <param name="elevationRequests">The processes or services to kill before the operation can be run</param>
        /// <param name="cancellationToken"></param>
        /// <returns>All the environment variables to be set</returns>
        /// <remarks><see cref="StartRemoteAgentAsync(CancellationToken)"/> need to be invoked before this.</remarks>
        Task<IDictionary<string, string>> GetLocalEnvironment(int[] localPorts, CancellationToken cancellationToken);

        /// <summary>
        /// Invokes the EndpointManager
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StartEndpointManagerAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops port forwarding and resets the host mapping.
        /// This resets the local network part of the connection.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StopLocalConnectionAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Stops and remove the LocalAgent container
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task StopLocalAgentAsync(string localAgentContainerName, CancellationToken cancellationToken);

        #endregion LocalMachine
    }
}