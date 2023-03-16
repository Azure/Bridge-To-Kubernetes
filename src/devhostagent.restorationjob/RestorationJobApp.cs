// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models.DevHost;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Restore;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob.Logging;
using SystemTextJsonPatch;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using static Microsoft.BridgeToKubernetes.Common.DevHostAgent.DevHostConstants;
using static Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob.Logging.Events.RestorationJob.Properties;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob
{
    /// <summary>
    /// Main logic for the restoration job
    /// </summary>
    internal class RestorationJobApp : AppBase
    {
        /// <summary>
        /// Format string for hitting the devhostagent connected sessions API
        /// </summary>
        private static readonly string AgentPingEndpointFormat = $"http://{{0}}:{DevHostConstants.DevHostAgent.Port}/api/connectedsessions";

        private readonly ILog _log;
        private readonly IWorkloadRestorationService _workloadRestorationService;
        private readonly IRemoteRestoreJobCleaner _remoteRestoreJobCleaner;
        private readonly IKubernetesClient _kubernetesClient;
        private readonly IFileSystem _fileSystem;
        private readonly IRestorationJobEnvironmentVariables _restorationJobEnvironmentVariables;
        private readonly HttpClient _httpClient;

        /// <summary>
        /// Constructor
        /// </summary>
        public RestorationJobApp(
            ILog log,
            IWorkloadRestorationService workloadRestorationService,
            IRemoteRestoreJobCleaner remoteRestoreJobCleaner,
            IKubernetesClient kubernetesClient,
            IFileSystem fileSystem,
            IRestorationJobEnvironmentVariables restorationJobEnvironmentVariables,
            HttpClient httpClient)
        {
            _log = log;
            _workloadRestorationService = workloadRestorationService;
            _remoteRestoreJobCleaner = remoteRestoreJobCleaner;
            _kubernetesClient = kubernetesClient;
            _fileSystem = fileSystem;
            _restorationJobEnvironmentVariables = restorationJobEnvironmentVariables;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Public synchronous execution method
        /// </summary>
        public override int Execute(string[] args, CancellationToken cancellationToken)
            => (int)AsyncHelpers.RunSync(() => this.ExecuteInnerAsync(cancellationToken));

        /// <summary>
        /// Execution implementation
        /// </summary>
        private async Task<ExitCode> ExecuteInnerAsync(CancellationToken cancellationToken)
        {
            try
            {
                AssertHelper.NotNullOrEmpty(_restorationJobEnvironmentVariables.Namespace, nameof(_restorationJobEnvironmentVariables.Namespace));
                AssertHelper.NotNullOrEmpty(_restorationJobEnvironmentVariables.InstanceLabelValue, nameof(_restorationJobEnvironmentVariables.InstanceLabelValue));

                // Load patch state
                var patchState = this._ParsePatchState();

                _log.Info("Waiting to restore previous state on {0} {1}/{2}...", patchState.KubernetesType.GetStringValue(), new PII(patchState.Namespace), new PII(patchState.Name));
                // Extra wait at the beginning to allow things to initialize
                await Task.Delay(_restorationJobEnvironmentVariables.PingInterval, cancellationToken);
                int numFailedPings = 0;
                DateTimeOffset? lastPingWithSessions = null;
                DateTimeOffset? timeSinceLastPingIsNull = null;
                bool restoredWorkload = false;
                while (!cancellationToken.IsCancellationRequested && !restoredWorkload)
                {
                    if (numFailedPings >= _restorationJobEnvironmentVariables.NumFailedPingsBeforeExit)
                    {
                        _log.Error($"Failed to ping agent {numFailedPings} times. Exiting...");
                        return ExitCode.Fail;
                    }

                    // Sleep
                    await Task.Delay(_restorationJobEnvironmentVariables.PingInterval, cancellationToken);

                    _log.Verbose("Pinging...");
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.RestorationJob.AreaName,
                        Events.RestorationJob.Operations.AgentPing,
                        new Dictionary<string, object>()
                        {
                            { RestorePerformed, false },
                            { NumFailedPings, numFailedPings },
                            { HasConnectedClients, "" }
                        }))
                    {
                        // Get agent endpoint
                        Uri agentEndpoint = await this._GetAgentEndpointAsync((dynamic)patchState, cancellationToken);
                        if (agentEndpoint == null)
                        {
                            _log.Verbose("Couldn't get agent endpoint");
                            numFailedPings++;
                            continue;
                        }

                        // Ping agent
                        var result = await this._PingAgentAsync(agentEndpoint, cancellationToken);
                        if (result == null)
                        {
                            _log.Verbose("Failed to ping agent");
                            numFailedPings++;
                            continue;
                        }
                        else if (result.NumConnectedSessions > 0)
                        {
                            _log.Verbose($"Agent has {result.NumConnectedSessions} connected sessions");
                            lastPingWithSessions = DateTimeOffset.Now;
                            perfLogger.SetProperty(HasConnectedClients, true);
                        }
                        else
                        {
                            perfLogger.SetProperty(HasConnectedClients, false);
                            TimeSpan? disconnectedTimeSpan = null;
                            if (lastPingWithSessions == null)
                            {
                                // first loop timeUntilLastPingIsNull will be set to current time and then next while loop it will preserve that time.
                                // if lastPingWithSessions is being null for last 60 seconds or more then restoration will happen.
                                timeSinceLastPingIsNull = timeSinceLastPingIsNull == null ? DateTimeOffset.Now : timeSinceLastPingIsNull;
                                disconnectedTimeSpan = DateTimeOffset.Now - timeSinceLastPingIsNull;
                            } else
                            {
                                disconnectedTimeSpan = DateTimeOffset.Now - lastPingWithSessions;
                            }

                            
                            if (disconnectedTimeSpan != null && disconnectedTimeSpan.Value > _restorationJobEnvironmentVariables.RestoreTimeout)
                            {
                                // Restore workload
                                _log.Info($"Agent has no connected sessions for {disconnectedTimeSpan.Value:g}. Restoring...");
                                await this._RestoreAsync((dynamic)patchState, cancellationToken);
                                _log.Info("Restored {0} {1}/{2}.", patchState.KubernetesType.GetStringValue(), new PII(patchState.Namespace), new PII(patchState.Name));
                                perfLogger.SetProperty(RestorePerformed, true);
                                restoredWorkload = true;
                            }
                        }

                        numFailedPings = 0;
                        perfLogger.SetSucceeded();
                    }
                }

                if (restoredWorkload)
                {
                    // Clean up restoration job
                    // Don't pass cancellationToken because we don't want a race condition for Kubernetes killing the pod
                    await _remoteRestoreJobCleaner.CleanupRemoteRestoreJobByInstanceLabelAsync(_restorationJobEnvironmentVariables.InstanceLabelValue, _restorationJobEnvironmentVariables.Namespace, default(CancellationToken));
                    return ExitCode.Success;
                }

                // If we get here, that means the cancellationToken must have been canceled.
                return ExitCode.Cancel;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _log.Info("Restoration job cancelled");
                return ExitCode.Cancel;
            }
            catch (Exception e)
            {
                _log.WithoutTelemetry.Error($"Encountered exception: {e.Message}");
                _log.WithoutConsole.Exception(e);
                return ExitCode.Fail;
            }
        }

        private async Task<ConnectedSessionsResponseModel> _PingAgentAsync(Uri agentEndpoint, CancellationToken cancellationToken)
        {
            try
            {
                _log.Verbose($"Pinging agent at '{agentEndpoint}'");
                using (var response = await _httpClient.GetAsync(agentEndpoint, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    string rawContent = await response.Content.ReadAsStringAsync();
                    var content = JsonHelpers.DeserializeObject<ConnectedSessionsResponseModel>(rawContent);
                    return content;
                }
            }
            catch (HttpRequestException e)
            {
                _log.Warning($"{nameof(HttpRequestException)} pinging agent '{agentEndpoint}': {e.Message}");
                _log.ExceptionAsWarning(e);
                return null;
            }
        }

        /// <summary>
        /// Parses the mounted patch state JSON
        /// </summary>
        private PatchEntityBase _ParsePatchState()
        {
            string patchStateJson = _fileSystem.ReadAllTextFromFile(DevHostRestorationJob.PatchStateFullPath);
            string type = JsonHelpers.ParseAndGetProperty<string>(patchStateJson, typeof(PatchEntityBase).GetJsonPropertyName(nameof(PatchEntityBase.Type)));
            return type switch
            {
                nameof(DeploymentPatch) => JsonHelpers.DeserializeObject<DeploymentPatch>(patchStateJson),
                nameof(PodPatch) => JsonHelpers.DeserializeObject<PodPatch>(patchStateJson),
                nameof(PodDeployment) => JsonHelpers.DeserializeObject<PodDeployment>(patchStateJson),
                nameof(StatefulSetPatch) => JsonHelpers.DeserializeObject<StatefulSetPatch>(patchStateJson),
                _ => throw new InvalidOperationException($"Unknown restoration patch type: '{type}'"),
            };
        }

        #region Get agent endpoint

        /// <summary>
        /// Get the agent ping endpoint for a <see cref="DeploymentPatch"/> operation
        /// </summary>
        private async Task<Uri> _GetAgentEndpointAsync(DeploymentPatch deploymentPatch, CancellationToken cancellationToken)
        {
            string ns = deploymentPatch.Deployment.Namespace();
            string name = deploymentPatch.Deployment.Name();

            try
            {
                var pods = await _kubernetesClient.ListPodsForDeploymentAsync(ns, name, cancellationToken);
                if (pods?.Items == null)
                {
                    _log.Warning("Deployment {0}/{1} not found", new PII(ns), new PII(name));
                    return null;
                }
                else if (pods.Items.Count != 1)
                {
                    _log.Warning("Found {0} pods for deployment {1}/{2} but expected 1", pods.Items.Count, new PII(ns), new PII(name));
                    return null;
                }

                var devhostAgentPod = pods.Items.Single();
                if (devhostAgentPod.Spec.Containers.Select(c => c.Image).Contains(deploymentPatch.ReversePatch.TryGetContainerImageReplacementValue()))
                {
                    _log.Warning("Deployment {0}/{1} has already been restored", new PII(ns), new PII(name));
                    return null;
                }

                if (string.IsNullOrWhiteSpace(devhostAgentPod.Status.PodIP))
                {
                    _log.Warning("DevhostAgentPod IP was null");
                    return null;
                }

                return new Uri(string.Format(AgentPingEndpointFormat, devhostAgentPod.Status.PodIP));
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                _log.Exception(e);
                return null;
            }
        }

        /// <summary>
        /// Get the agent ping endpoint for a <see cref="StatefulSetPatch"/> operation
        /// </summary>
        private async Task<Uri> _GetAgentEndpointAsync(StatefulSetPatch statefulSetPatch, CancellationToken cancellationToken)
        {
            string ns = statefulSetPatch.StatefulSet.Namespace();
            string name = statefulSetPatch.StatefulSet.Name();

            try
            {
                var pods = await _kubernetesClient.ListPodsForStatefulSetAsync(ns, name, cancellationToken);
                if (pods?.Items == null)
                {
                    _log.Warning("StatefulSet {0}/{1} not found", new PII(ns), new PII(name));
                    return null;
                }
                else if (pods.Items.Count != 1)
                {
                    _log.Warning("Found {0} pods for StatefulSet {1}/{2} but expected 1", pods.Items.Count, new PII(ns), new PII(name));
                    return null;
                }

                var devhostAgentPod = pods.Items.Single();
                if (devhostAgentPod.Spec.Containers.Select(c => c.Image).Contains(statefulSetPatch.ReversePatch.TryGetContainerImageReplacementValue()))
                {
                    _log.Warning("StatefulSet {0}/{1} has already been restored", new PII(ns), new PII(name));
                    return null;
                }

                if (string.IsNullOrWhiteSpace(devhostAgentPod.Status.PodIP))
                {
                    _log.Warning("DevhostAgentPod IP was null");
                    return null;
                }

                return new Uri(string.Format(AgentPingEndpointFormat, devhostAgentPod.Status.PodIP));
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                _log.Exception(e);
                return null;
            }
        }

        /// <summary>
        /// Get the agent ping endpoint for a <see cref="PodPatch"/> operation
        /// </summary>
        private async Task<Uri> _GetAgentEndpointAsync(PodPatch podPatch, CancellationToken cancellationToken)
        {
            string ns = podPatch.Pod.Namespace();
            string name = podPatch.Pod.Name();
            try
            {
                var pod = await _kubernetesClient.GetV1PodAsync(ns, name, cancellationToken);
                if (pod == null)
                {
                    _log.Warning("Couldn't find patched pod {0}/{1}", new PII(ns), new PII(name));
                    return null;
                }
                else if (pod.Spec.Containers.Select(c => c.Image).Contains(podPatch.ReversePatch.TryGetContainerImageReplacementValue()))
                {
                    _log.Warning("Pod {0}/{1} has already been restored", new PII(ns), new PII(name));
                    return null;
                }

                return new Uri(string.Format(AgentPingEndpointFormat, pod.Status.PodIP));
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                _log.Exception(e);
                return null;
            }
        }

        /// <summary>
        /// Get the agent ping endpoint for a <see cref="PodDeployment"/> operation
        /// </summary>
        private async Task<Uri> _GetAgentEndpointAsync(PodDeployment podDeployment, CancellationToken cancellationToken)
        {
            string ns = podDeployment.Pod.Namespace();
            string name = podDeployment.Pod.Name();
            try
            {
                var pod = await _kubernetesClient.GetV1PodAsync(ns, name, cancellationToken);
                if (pod == null)
                {
                    _log.Warning("Couldn't find deployed pod {0}/{1}", new PII(ns), new PII(name));
                    return null;
                }

                return new Uri(string.Format(AgentPingEndpointFormat, pod.Status.PodIP));
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                _log.Exception(e);
                return null;
            }
        }

        #endregion Get agent endpoint

        #region Restore

        /// <summary>
        /// Restores a <see cref="DeploymentPatch"/>
        /// </summary>
        public Task _RestoreAsync(DeploymentPatch deploymentPatch, CancellationToken cancellationToken)
            => _workloadRestorationService.RestoreDeploymentPatchAsync(deploymentPatch, cancellationToken, m => _log.Info(m.Message));

        /// <summary>
        /// Restores a <see cref="StatefulSetPatch"/>
        /// </summary>
        public Task _RestoreAsync(StatefulSetPatch statefulSetPatch, CancellationToken cancellationToken)
            => _workloadRestorationService.RestoreStatefulSetPatchAsync(statefulSetPatch, cancellationToken, m => _log.Info(m.Message));

        /// <summary>
        /// Restores a <see cref="PodPatch"/>
        /// </summary>
        public Task _RestoreAsync(PodPatch podPatch, CancellationToken cancellationToken)
            => _workloadRestorationService.RestorePodPatchAsync(podPatch, cancellationToken, m => _log.Info(m.Message));

        /// <summary>
        /// Restores a <see cref="PodDeployment"/>
        /// </summary>
        public Task _RestoreAsync(PodDeployment podDeployment, CancellationToken cancellationToken)
            => _workloadRestorationService.RemovePodDeploymentAsync(podDeployment, cancellationToken, m => _log.Info(m.Message));

        #endregion Restore
    }
}