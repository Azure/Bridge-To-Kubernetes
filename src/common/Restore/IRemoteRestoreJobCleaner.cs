// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Restore
{
    /// <summary>
    /// Handles the cleanup of the devhostagent.restorationjob
    /// </summary>
    internal interface IRemoteRestoreJobCleaner
    {
        /// <summary>
        /// Removes the devhostagent.restorationjob for the given target workload
        /// </summary>
        Task CleanupRemoteRestoreJobAsync(string targetName, string namespaceName, CancellationToken cancellationToken);

        /// <summary>
        /// Removes the devhostagent.restorationjob tagged with the given instance label value
        /// </summary>
        Task CleanupRemoteRestoreJobByInstanceLabelAsync(string instanceLabelValue, string namespaceName, CancellationToken cancellationToken);
    }
}