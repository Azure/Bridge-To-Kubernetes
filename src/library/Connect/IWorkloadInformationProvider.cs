// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Manages services and other objects on the remote environment
    /// </summary>
    internal interface IWorkloadInformationProvider
    {
        /// <summary>
        /// The environment variables of the workload running in the cluster.
        /// </summary>
        AsyncLazy<IDictionary<string, string>> WorkloadEnvironment { get; }

        /// <summary>
        /// Get reachable services for the target.
        /// </summary>
        Task<IEnumerable<EndpointInfo>> GetReachableEndpointsAsync(
            string namespaceName,
            ILocalProcessConfig localProcessConfig,
            bool includeSameNamespaceServices,
            CancellationToken cancellationToken);

        /// <summary>
        /// Retrieve the in-cluster context/environment the remoteAgent container is running with.
        /// </summary>
        /// <remarks>This is the new version, where we pull the data BEFORE we deploy the remote agent. This way it is simpler and we avoid potential conflicts with what the remote agent sets for itself.</remarks>
        Task<WorkloadInfo> GatherWorkloadInfo(
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken);
    }
}