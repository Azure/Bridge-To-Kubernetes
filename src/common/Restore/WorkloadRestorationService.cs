// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using System;
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using SystemTextJsonPatch;
using static Microsoft.BridgeToKubernetes.Common.Logging.CommonEvents.WorkloadRestorationService;

namespace Microsoft.BridgeToKubernetes.Common.Restore
{
    /// <summary>
    /// <see cref="IWorkloadRestorationService"/>
    /// </summary>
    internal class WorkloadRestorationService : IWorkloadRestorationService
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly ILog _log;

        public WorkloadRestorationService(IKubernetesClient kubernetesClient, ILog log)
        {
            _kubernetesClient = kubernetesClient;
            _log = log;
        }

        /// <summary>
        /// <see cref="IWorkloadRestorationService.RestorePodPatchAsync"/>
        /// </summary>
        public Task RestorePodPatchAsync(PodPatch podPatch, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false)
        {
            var patchString = JsonHelpers.SerializeObject(podPatch.ReversePatch);
            string originalImage = podPatch.ReversePatch.TryGetContainerImageReplacementValue();

            return _RestoreAsync(
                operation: Operations.RestorePod,
                exceptionMsg: string.Format(CommonResources.RestorePodRestoreFailedWithImageFormat, podPatch.Pod.Name(), originalImage),
                progressCallback,
                noThrow,
                cancellationToken,
                async () =>
                {
                    V1Pod pod = await this._kubernetesClient.GetV1PodAsync(podPatch.Pod.Namespace(), podPatch.Pod.Name(), cancellationToken: cancellationToken);
                    if (pod == null)
                    {
                        return;   // the pod may not be running anymore
                    }
                    await _kubernetesClient.PatchV1PodAsync(pod.Namespace(), pod.Name(), new V1Patch(patchString, V1Patch.PatchType.JsonPatch), cancellationToken: cancellationToken);
                    this._ReportProgress(progressCallback, CommonResources.RestorePodRestoredWithImageFormat, pod.Name(), originalImage);
                });
        }

        /// <summary>
        /// <see cref="IWorkloadRestorationService.RemovePodDeploymentAsync"/>
        /// </summary>
        public Task RemovePodDeploymentAsync(PodDeployment podDeployment, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false)
        {
            var pod = podDeployment.Pod;

            return _RestoreAsync(
                operation: Operations.RemovePodDeployment,
                exceptionMsg: string.Format(CommonResources.RestoreFailedToRestoreResourceFormat, "pod", podDeployment.UserPodToRestore.Name()),
                progressCallback,
                noThrow,
                cancellationToken,
                async () =>
                {
                    if (podDeployment.UserPodToRestore == null)
                    {
                        await _kubernetesClient.DeleteV1PodAsync(pod.Namespace(), pod.Name(), cancellationToken: cancellationToken);
                        this._ReportProgress(progressCallback, CommonResources.RestorePodDeletedFormat, podDeployment.Pod.Name());
                        return;
                    }

                    // We replaced a user pod, so we need to wait until our deployed pod is gone to restore it.
                    var isPodDeleted = await WebUtilities.RetryUntilTimeWithWaitAsync(async (t) =>
                    {
                        try
                        {
                            await _kubernetesClient.DeleteV1PodAsync(pod.Namespace(), pod.Name(), cancellationToken: cancellationToken);
                            return (await this._kubernetesClient.GetV1PodAsync(pod.Namespace(), pod.Name(), cancellationToken: cancellationToken)) == null;
                        }
                        catch
                        {
                            return false;
                        }
                    },
                    maxWaitTime: TimeSpan.FromMinutes(5),
                    waitInterval: TimeSpan.FromMilliseconds(500),
                    cancellationToken: cancellationToken);

                    if (!isPodDeleted)
                    {
                        throw new InvalidOperationException(string.Format(CommonResources.RestoreFailedToDeletePodFormat, pod.Namespace(), pod.Name()));
                    }
                    this._ReportProgress(progressCallback, CommonResources.RestorePodDeletedFormat, podDeployment.Pod.Name());

                    var deployedPod = await _kubernetesClient.CreateV1PodAsync(podDeployment.UserPodToRestore.Namespace(), podDeployment.UserPodToRestore, cancellationToken: cancellationToken);
                    this._ReportProgress(progressCallback, CommonResources.RestorePodRestoredFormat, podDeployment.UserPodToRestore.Name());
                });
        }

        /// <summary>
        /// <see cref="IWorkloadRestorationService.RestoreDeploymentPatchAsync"/>
        /// </summary>
        public Task RestoreDeploymentPatchAsync(DeploymentPatch deploymentPatch, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false)
        {
            var deployment = deploymentPatch.Deployment;
            var reversePatch = deploymentPatch.ReversePatch;
            string originalImage = reversePatch.TryGetContainerImageReplacementValue();
            var patchString = JsonHelpers.SerializeObject(reversePatch);

            return _RestoreAsync(
                operation: Operations.RestoreDeployment,
                exceptionMsg: string.Format(CommonResources.RestoreFailedToRestoreResourceWithImageFormat, KubernetesResourceType.Deployment.ToString(), deployment.Name(), originalImage),
                progressCallback,
                noThrow,
                cancellationToken,
                async () =>
                {
                    await _kubernetesClient.PatchV1DeploymentAsync(deployment.Namespace(), deployment.Name(), new V1Patch(patchString, V1Patch.PatchType.JsonPatch), cancellationToken: cancellationToken);
                    if (string.IsNullOrEmpty(originalImage))
                    {
                        this._ReportProgress(progressCallback, CommonResources.RestoreResourceRestoredFormat, KubernetesResourceType.Deployment.ToString(), deployment.Name());
                    }
                    else
                    {
                        this._ReportProgress(progressCallback, CommonResources.RestoreResourceRestoredWithImageFormat, KubernetesResourceType.Deployment.ToString(), deployment.Name(), originalImage);
                    }
                });
        }

        /// <summary>
        /// <see cref="IWorkloadRestorationService.RestoreStatefulSetPatchAsync"/>
        /// </summary>
        public Task RestoreStatefulSetPatchAsync(StatefulSetPatch statefulSetPatch, CancellationToken cancellationToken, Action<ProgressMessage> progressCallback = null, bool noThrow = false)
        {
            var statefulSet = statefulSetPatch.StatefulSet;
            var reversePatch = statefulSetPatch.ReversePatch;
            string originalImage = reversePatch.TryGetContainerImageReplacementValue();
            var patchString = JsonHelpers.SerializeObject(reversePatch);

            return _RestoreAsync(
                operation: Operations.RestoreStatefulSet,
                exceptionMsg: string.Format(CommonResources.RestoreFailedToRestoreResourceWithImageFormat, KubernetesResourceType.StatefulSet.ToString(), statefulSet.Name(), originalImage),
                progressCallback,
                noThrow,
                cancellationToken,
                async () =>
                {
                    await _kubernetesClient.PatchV1StatefulSetAsync(statefulSet.Namespace(), statefulSet.Name(), new V1Patch(patchString, V1Patch.PatchType.JsonPatch), cancellationToken: cancellationToken);
                    if (string.IsNullOrEmpty(originalImage))
                    {
                        this._ReportProgress(progressCallback, CommonResources.RestoreResourceRestoredFormat, KubernetesResourceType.StatefulSet.ToString(), statefulSet.Name());
                    }
                    else
                    {
                        this._ReportProgress(progressCallback, CommonResources.RestoreResourceRestoredWithImageFormat, KubernetesResourceType.StatefulSet.ToString(), statefulSet.Name(), originalImage);
                    }
                });
        }

        /// <summary>
        /// Internal wrapper that manages the perfLogger and common exception handling
        /// </summary>
        private async Task _RestoreAsync(string operation, string exceptionMsg, Action<ProgressMessage> progressCallback, bool noThrow, CancellationToken cancellationToken, Func<Task> restoreTask)
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                AreaName,
                operation))
            {
                try
                {
                    await restoreTask();
                    perfLogger.SetSucceeded();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    perfLogger.SetCancelled();
                    throw;
                }
                catch (Exception ex)
                {
                    exceptionMsg = $"{exceptionMsg.Trim(' ', '.')}. {ex.Message}";
                    if (!noThrow)
                    {
                        throw new UserVisibleException(_log.OperationContext, ex, exceptionMsg);
                    }
                    this._log.Exception(ex);
                    this._ReportProgress(progressCallback, exceptionMsg, EventLevel.Error);
                }
            }
        }

        /// <summary>
        /// Progress reporter for <see cref="WorkloadRestorationService"/>
        /// </summary>
        private void _ReportProgress(Action<ProgressMessage> progressCallback, string message, params object[] args)
        {
            progressCallback?.Invoke(new ProgressMessage(EventLevel.Informational, _log.SaferFormat(message, args)));
        }

        /// <summary>
        /// Progress reporter for <see cref="WorkloadRestorationService"/>
        /// </summary>
        private void _ReportProgress(Action<ProgressMessage> progressCallback, EventLevel eventLevel, string message, params object[] args)
        {
            progressCallback?.Invoke(new ProgressMessage(eventLevel, _log.SaferFormat(message, args)));
        }
    }
}