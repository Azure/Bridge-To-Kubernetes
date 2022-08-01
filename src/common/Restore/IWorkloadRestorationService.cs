// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;

namespace Microsoft.BridgeToKubernetes.Common.Restore
{
    /// <summary>
    /// Service used to restore the state of workloads after a Connect session
    /// </summary>
    internal interface IWorkloadRestorationService
    {
        /// <summary>
        /// Restores a pod back to its original values based on the provided <see cref="PodPatch"/>
        /// </summary>
        /// <exception cref="Exceptions.UserVisibleException">When an error is encountered during restore</exception>
        /// <param name="podPatch"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="progressCallback"></param>
        /// <param name="noThrow">Whether to throw an exception on error or just log to the progressCallback</param>
        Task RestorePodPatchAsync(PodPatch podPatch, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false);

        /// <summary>
        /// Removes a pod deployment from the cluster based on the provided <see cref="PodDeployment"/>
        /// </summary>
        /// <exception cref="Exceptions.UserVisibleException">When an error is encountered during restore</exception>
        /// <param name="podDeployment"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="progressCallback"></param>
        /// <param name="noThrow">Whether to throw an exception on error or just log to the progressCallback</param>
        Task RemovePodDeploymentAsync(PodDeployment podDeployment, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false);

        /// <summary>
        /// Restores a deployment back to its original values based on the provided <see cref="DeploymentPatch"/>
        /// </summary>
        /// <exception cref="Exceptions.UserVisibleException">When an error is encountered during restore</exception>
        /// <param name="deploymentPatch"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="progressCallback"></param>
        /// <param name="noThrow">Whether to throw an exception on error or just log to the progressCallback</param>
        Task RestoreDeploymentPatchAsync(DeploymentPatch deploymentPatch, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false);

        /// <summary>
        /// Restores a statefulSet back to its original values based on the provided <see cref="StatefulSetPatch"/>
        /// </summary>
        /// <exception cref="Exceptions.UserVisibleException">When an error is encountered during restore</exception>
        /// <param name="statefulSetPatch"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="progressCallback"></param>
        /// <param name="noThrow">Whether to throw an exception on error or just log to the progressCallback</param>
        Task RestoreStatefulSetPatchAsync(StatefulSetPatch statefulSetPatch, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false);
    }
}