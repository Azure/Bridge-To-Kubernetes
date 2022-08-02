// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.Restore
{
    internal class RemoteRestoreJobCleaner : IRemoteRestoreJobCleaner
    {
        protected readonly IKubernetesClient _kubernetesClient;
        protected readonly ILog _log;

        public RemoteRestoreJobCleaner(
            IKubernetesClient kubernetesClient,
            ILog log)
        {
            _kubernetesClient = kubernetesClient;
            _log = log;
        }

        /// <summary>
        /// <see cref="IRemoteRestoreJobCleaner.CleanupRemoteRestoreJobAsync"/>
        /// </summary>
        public Task CleanupRemoteRestoreJobAsync(string targetName, string namespaceName, CancellationToken cancellationToken)
        {
            string instanceLabelValue = GetInstanceLabel(targetName);
            return this.CleanupRemoteRestoreJobByInstanceLabelAsync(instanceLabelValue, namespaceName, cancellationToken);
        }

        public async Task CleanupRemoteRestoreJobByInstanceLabelAsync(string instanceLabelValue, string namespaceName, CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                CommonEvents.RemoteRestoreJobCleaner.AreaName,
                CommonEvents.RemoteRestoreJobCleaner.Operations.Cleanup))
            {
                try
                {
                    int exitCode = await CleanupInnerAsync(instanceLabelValue, namespaceName, cancellationToken);
                    if (exitCode == 0)
                    {
                        _log.Info("Cleaned up restore job");
                        perfLogger.SetSucceeded();
                    }
                    else
                    {
                        _log.Warning("Couldn't cleanup restore job. Job either not found, or something went wrong.");
                        perfLogger.SetBadRequest();
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    perfLogger.SetCancelled();
                    throw;
                }
            }
        }

        /// <summary>
        /// Inner cleanup method that deletes restore job resources associated with a specific instance
        /// </summary>
        protected async Task<int> CleanupInnerAsync(string instanceLabelValue, string namespaceName, CancellationToken cancellationToken)
        {
            int exitCode = 0;
            await WebUtilities.RetryUntilTimeAsync(async (i) =>
            {
                exitCode = await _kubernetesClient.InvokeShortRunningKubectlCommandAsync(
                    KubernetesCommandName.Delete,
                    $"delete job,secret -n {namespaceName} -l {Labels.InstanceLabelName}={instanceLabelValue}",
                    shouldIgnoreErrors: true,
                    logOutput: true,
                    timeoutMs: (int)TimeSpan.FromSeconds(20).TotalMilliseconds,
                    cancellationToken: cancellationToken);

                return exitCode == 0;
            }, TimeSpan.FromSeconds(30), cancellationToken);

            return exitCode;
        }

        /// <summary>
        /// Gets the value of this instance's <see cref="Labels.InstanceLabelName"/> label
        /// </summary>
        protected static string GetInstanceLabel(string targetName)
            => targetName.Sha256Hash(10);
    }
}