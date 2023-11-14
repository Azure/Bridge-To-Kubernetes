// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections;
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
                (PatchEntityBase, Func<PatchEntityBase, CancellationToken, Task<List<Uri>>>) patchState = ParsePatchState(cancellationToken);

                _log.Info("Waiting to restore previous state on {0} {1}/{2}...", patchState.Item1.KubernetesType.GetStringValue(), new PII(patchState.Item1.Namespace), new PII(patchState.Item1.Name));
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
                        var patch = patchState.Item1;
                        List<Uri> agentEndpoint = await patchState.Item2(patch, cancellationToken);
                        if (agentEndpoint == null || agentEndpoint.Count == 0)
                        {
                            _log.Verbose("Couldn't get agent endpoint");
                            numFailedPings++;
                            continue;
                        }


                        // Ping agent
                        ConnectedSessionsResponseModel[] results = await Task
                        .WhenAll(agentEndpoint.Select(uri => PingAgentAsync(uri, cancellationToken))
                        .ToList()
                        .Where(content => null != content));

                        if (results == null || results.Length == 0)
                        {
                            _log.Verbose("Failed to ping agent");
                            numFailedPings++;
                            continue;
                        }

                        bool isAnyConnectedSession = results.ToList().Any(result => result.NumConnectedSessions > 0);

                        int numConnectedSessions = results.ToList().Sum(result => result.NumConnectedSessions);

                        if (isAnyConnectedSession && numConnectedSessions > 0)
                        {
                            _log.Verbose($"Agent has {numConnectedSessions} connected sessions");
                            lastPingWithSessions = DateTimeOffset.Now;
                            perfLogger.SetProperty(HasConnectedClients, true);
                        }
                        else
                        {
                            perfLogger.SetProperty(HasConnectedClients, false);
                            TimeSpan? disconnectedTimeSpan = lastPingWithSessions.HasValue
                                ? DateTimeOffset.Now - lastPingWithSessions.Value
                                : DateTimeOffset.Now - (timeSinceLastPingIsNull ??= DateTimeOffset.Now); //??= is equivalent to checking if value == null ? value2 : value

                            if (disconnectedTimeSpan != null && disconnectedTimeSpan.Value > _restorationJobEnvironmentVariables.RestoreTimeout)
                            {
                                // Restore workload
                                _log.Info($"Agent has no connected sessions for {disconnectedTimeSpan.Value:g}. Restoring...");
                                await this.RestoreAsync(patch, cancellationToken);
                                _log.Info("Restored {0} {1}/{2}.", patchState.Item1.KubernetesType.GetStringValue(), new PII(patchState.Item1.Namespace), new PII(patchState.Item1.Name));
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

        private async Task<ConnectedSessionsResponseModel> PingAgentAsync(Uri agentEndpoint, CancellationToken cancellationToken)
        {
            try
            {
                _log.Verbose($"Pinging agent at '{agentEndpoint}'");
                using (var response = await _httpClient.GetAsync(agentEndpoint, cancellationToken))
                {
                    response.EnsureSuccessStatusCode();
                    string rawContent = await response.Content.ReadAsStringAsync(cancellationToken);
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
        private (PatchEntityBase, Func<PatchEntityBase, CancellationToken, Task<List<Uri>>>) ParsePatchState(CancellationToken cancellationToken)
        {
            string patchStateJson = _fileSystem.ReadAllTextFromFile(DevHostRestorationJob.PatchStateFullPath);
            string type = JsonPropertyHelpers.ParseAndGetProperty<string>(patchStateJson, typeof(PatchEntityBase).GetJsonPropertyName(nameof(PatchEntityBase.Type)));

            switch (type)
            {
                case nameof(DeploymentPatch):
                    var deploymentPatch = JsonHelpers.DeserializeObject<DeploymentPatch>(patchStateJson);
                    return (deploymentPatch, (patch, ct) => GetAgentEndpointAsync(deploymentPatch, ct));
                case nameof(PodPatch):
                    var podPatch = JsonHelpers.DeserializeObject<PodPatch>(patchStateJson);
                    return (podPatch, (patch, ct) => GetAgentEndpointAsync(podPatch, ct));
                case nameof(PodDeployment):
                    var podDeployment = JsonHelpers.DeserializeObject<PodDeployment>(patchStateJson);
                    return (podDeployment, (patch, ct) => GetAgentEndpointAsync(podDeployment, ct));
                case nameof(StatefulSetPatch):
                    var statefulSet = JsonHelpers.DeserializeObject<StatefulSetPatch>(patchStateJson);
                    return (statefulSet, (patch, ct) => GetAgentEndpointAsync(statefulSet, ct));
                default:
                    throw new InvalidOperationException($"Unknown restoration patch type: '{type}'");
            }
        }
        #region Get agent endpoint

        /// <summary>
        /// Get the agent ping endpoint for a <see cref="DeploymentPatch"/> operation
        /// </summary>
        private async Task<List<Uri>> GetAgentEndpointAsync(DeploymentPatch deploymentPatch, CancellationToken cancellationToken)
        {
            string ns = deploymentPatch.Deployment.Namespace();
            string name = deploymentPatch.Deployment.Name();
            List<Uri> uriList = new();
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
                    // return all the pod's IP, probably this is multiple replica's for a pod.
                    // confirm it is multiple replica's for a pod and owner of the pod name.
                    var uris = pods.Items
                        .Where(pod => pod.Metadata.OwnerReferences.All(r => r.Kind == "ReplicaSet") && pod.Metadata.OwnerReferences.All(r => r.Name.StartsWith(name)))
                        .Where(pod => !string.IsNullOrWhiteSpace(pod.Status.PodIP))
                        .Select(pod => new Uri(string.Format(AgentPingEndpointFormat, pod.Status.PodIP)))
                        .ToList();

                    if (uris.Count == 0)
                    {
                        _log.Warning("Unable to find any pod with owner reference as ReplicaSet and name starts with {0}", new PII(name));
                        return null;
                    }

                    uriList.AddRange(uris);
                    return uriList;
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

                uriList.Add(new Uri(string.Format(AgentPingEndpointFormat, devhostAgentPod.Status.PodIP)));
                return uriList;

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
        private async Task<List<Uri>> GetAgentEndpointAsync(StatefulSetPatch statefulSetPatch, CancellationToken cancellationToken)
        {
            string ns = statefulSetPatch.StatefulSet.Namespace();
            string name = statefulSetPatch.StatefulSet.Name();
            List<Uri> uriList = new();
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
                    // return all the pod's IP, probably this is multiple replica's for a pod.
                    // confirm it is multiple replica's for a pod and owner of the pod name.
                    var uris = pods.Items
                        .Where(pod => pod.Metadata.OwnerReferences.All(r => r.Kind == "StatefulSet") && pod.Metadata.OwnerReferences.All(r => r.Name.StartsWith(name)))
                        .Where(pod => !string.IsNullOrWhiteSpace(pod.Status.PodIP))
                        .Select(pod => new Uri(string.Format(AgentPingEndpointFormat, pod.Status.PodIP)))
                        .ToList();

                    if (uris.Count == 0)
                    {
                        _log.Warning("Unable to find any pod with owner reference as StatefulSet and name starts with {0}", new PII(name));
                        return null;
                    }

                    uriList.AddRange(uris);
                    return uriList;
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
                uriList.Add(new Uri(string.Format(AgentPingEndpointFormat, devhostAgentPod.Status.PodIP)));
                return uriList;
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
        private async Task<List<Uri>> GetAgentEndpointAsync(PodPatch podPatch, CancellationToken cancellationToken)
        {
            string ns = podPatch.Pod.Namespace();
            string name = podPatch.Pod.Name();
            List<Uri> uriList = new();
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

                uriList.Add(new Uri(string.Format(AgentPingEndpointFormat, pod.Status.PodIP)));
                return uriList;
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
        private async Task<List<Uri>> GetAgentEndpointAsync(PodDeployment podDeployment, CancellationToken cancellationToken)
        {
            string ns = podDeployment.Pod.Namespace();
            string name = podDeployment.Pod.Name();
            List<Uri> uriList = new();
            try
            {
                var pod = await _kubernetesClient.GetV1PodAsync(ns, name, cancellationToken);
                if (pod == null)
                {
                    _log.Warning("Couldn't find deployed pod {0}/{1}", new PII(ns), new PII(name));
                    return null;
                }

                uriList.Add(new Uri(string.Format(AgentPingEndpointFormat, pod.Status.PodIP)));
                return uriList;
            }
            catch (Exception e) when (!cancellationToken.IsCancellationRequested)
            {
                _log.Exception(e);
                return null;
            }
        }

        #endregion Get agent endpoint

        #region Restore

        public async Task RestoreAsync(PatchEntityBase patchEntityBase, CancellationToken cancellationToken) {
            switch(patchEntityBase.Type) {
                case nameof(DeploymentPatch):
                    await _workloadRestorationService.RestoreDeploymentPatchAsync((DeploymentPatch)patchEntityBase, cancellationToken, m => _log.Info(m.Message));
                    break;
                case nameof(PodPatch):
                    await _workloadRestorationService.RestorePodPatchAsync((PodPatch)patchEntityBase, cancellationToken, m => _log.Info(m.Message));
                    break;
                case nameof(PodDeployment):
                    await _workloadRestorationService.RemovePodDeploymentAsync((PodDeployment)patchEntityBase, cancellationToken, m => _log.Info(m.Message));
                    break;
                case nameof(StatefulSetPatch):
                    await _workloadRestorationService.RestoreStatefulSetPatchAsync((StatefulSetPatch)patchEntityBase, cancellationToken, m => _log.Info(m.Message));
                    break;
                default:
                    throw new ArgumentException($"Invalid patch entity type: {patchEntityBase.Type}");
            }
        }

        #endregion Restore
    }
}