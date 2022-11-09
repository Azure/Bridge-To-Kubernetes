// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Manages the environment to start un-containerized workload
    /// </summary>
    internal interface ILocalEnvironmentManager
    {
        /// <summary>
        /// Maps the reachableServices ports, in workloadInfo, to local ports and local IPs
        /// Updates the HostFile with the local IPs
        /// </summary>
        Task AddLocalMappingsAsync(
            WorkloadInfo workloadInfo,
            IEnumerable<IElevationRequest> elevationRequests,
            CancellationToken cancellationToken);

        /// <summary>
        /// Maps the reachableServices ports, in workloadInfo, to local ports and local IPs
        /// </summary>
        /// <remarks>Only free ports are used and the host file is not modified</remarks>
        void AddLocalMappingsUsingClusterEnvironmentVariables(
            WorkloadInfo workloadInfo,
            CancellationToken cancellationToken);

        /// <summary>
        /// Start the service port forwards long running tasks
        /// </summary>
        /// <param name="remoteAgentLocalPort"></param>
        /// <param name="reachableEndpoints"></param>
        /// <param name="cancellationToken"></param>
        void StartServicePortForwardings(
            int remoteAgentLocalPort,
            IEnumerable<EndpointInfo> reachableEndpoints,
            CancellationToken cancellationToken);

        /// <summary>
        /// Start the reverse port forwarding to route incoming calls from the remote agent to the user's workload
        /// </summary>
        /// <remarks>This is used when the user's workload runs natively, non-containerized</remarks>
        void StartReversePortForwarding(
            int remoteAgentLocalPort,
            IEnumerable<PortForwardStartInfo> reversePortForwardInfo,
            CancellationToken cancellationToken
            );

        /// <summary>
        /// Configure and starts the LocalAgent container that is going to serve as sidecar to the user's containerized workload.
        /// </summary>
        /// <remarks>This is used when the user's workload runs containerized</remarks>
        string StartLocalAgent(
            WorkloadInfo workloadInfo,
            KubeConfigDetails kubeConfigDetails,
            RemoteAgentInfo remoteAgentInfo);

        /// <summary>
        /// Stops the LocalAgent used as sidecar to the user's containerized workload.
        /// </summary>
        /// <remarks>This is used when the user's workload runs containerized</remarks>
        void StopLocalAgent(
            string localAgentContainerName);

        /// <summary>
        /// Start workload out of container with reverse port forwarding so that calls to remote agent container are routed to local workload.
        /// Returns a dictionary of environment variables to be used by local workload.
        /// </summary>
        Task<IDictionary<string, string>> GetLocalEnvironment(
            int remoteAgentLocalPort,
            WorkloadInfo workloadInfo,
            int[] localPorts,
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken);

        /// <summary>
        /// Stop the workload and service routers.
        /// </summary>
        Task StopAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Re-create the Kubernetes service related environment variables.
        /// </summary>
        public IDictionary<string, string> CreateEnvVariablesForK8s(
            WorkloadInfo workloadInfo);
    }
}