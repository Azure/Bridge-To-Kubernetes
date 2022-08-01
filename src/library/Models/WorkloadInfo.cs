// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// WorkloadInfo describes the environment of the user's workload.
    /// </summary>
    public class WorkloadInfo
    {
        /// <summary>
        /// Kubernetes namespace where the workload runs
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// The name of the workload (e.g. serviceName, podName, containerName)
        /// </summary>
        public string WorkloadName { get; set; }

        /// <summary>
        /// Workload entrypoint
        /// </summary>
        public string Entrypoint { get; set; }

        /// <summary>
        /// Workload entrypoint arguments
        /// </summary>
        public string[] Args { get; set; }

        /// <summary>
        /// Environment variables required by the workload
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }

        /// <summary>
        /// Inforamtion to start the reverse port forward
        /// </summary>
        public IEnumerable<PortForwardStartInfo> ReversePortForwardInfo { get; set; }

        /// <summary>
        /// Workload volumes
        /// </summary>
        public IEnumerable<ContainerVolumeMountInfo> VolumeMounts { get; set; }

        /// <summary>
        /// Reachable services
        /// </summary>
        public IEnumerable<EndpointInfo> ReachableEndpoints { get; set; }

        /// <summary>
        /// Working container
        /// </summary>
        public string WorkingContainer { get; set; }
    }
}