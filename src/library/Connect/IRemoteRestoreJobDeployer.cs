// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Restore;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Handles the deployment and cleanup of the devhostagent.restorationjob, which allows for remote restore fallback capabilities
    /// </summary>
    internal interface IRemoteRestoreJobDeployer : IRemoteRestoreJobCleaner
    {
        /// <summary>
        /// Ensures the devhostagent.restorationjob is running against a given target workload
        /// </summary>
        Task CreateRemoteRestoreJobAsync<T>(string targetName, string namespaceName, T patch, CancellationToken cancellationToken) where T : PatchEntityBase;

        /// <summary>
        /// Checks for the existence of patch info for a given target in the cluster
        /// </summary>
        Task<T> TryGetExistingPatchInfoAsync<T>(string targetName, string namespaceName, CancellationToken cancellationToken) where T : PatchEntityBase;
    }
}