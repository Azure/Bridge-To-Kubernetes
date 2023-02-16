// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.PortForward;
using Microsoft.BridgeToKubernetes.Common.Restore;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.Library.Utilities;
using Microsoft.Rest;
using Newtonsoft.Json.Serialization;
using static k8s.Models.V1Patch;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Implements <see cref="IRemoteEnvironmentManager"/>. Manages remote target service and related artifacts hosted on a Kubernetes platform.
    /// </summary>
    internal class KubernetesRemoteEnvironmentManager : IRemoteEnvironmentManager
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly ILog _log;
        private readonly IOperationContext _operationContext;
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly IImageProvider _imageProvider;
        private readonly IKubernetesPortForwardManager _portForwardManager;
        private readonly Lazy<IWorkloadRestorationService> _workloadRestorationService;
        private readonly IRemoteRestoreJobDeployer _remoteRestoreJobDeployer;
        private readonly DevHostAgentExecutorClient.Factory _devHostAgentExecutorClientFactory;
        private readonly IProgress<ProgressUpdate> _progress;

        // Container context
        private readonly PatchState _patchState = new PatchState();
        private V1Pod _remoteAgentPod;

        private AsyncLazy<RemoteContainerConnectionDetails> _remoteContainerConnectionDetails;

        // Remote agent information
        private int _devHostAgentLocalPort;
        private IDevHostAgentExecutorClient _devHostAgentExecutorClient;
        private CancellationTokenSource _devHostAgenPortForwardCancellationTokenSource = new CancellationTokenSource();

        public delegate IRemoteEnvironmentManager Factory(IKubernetesClient kubernetesClient, AsyncLazy<RemoteContainerConnectionDetails> connectionDetails);

        public KubernetesRemoteEnvironmentManager(
            IKubernetesClient kubernetesClient,
            AsyncLazy<RemoteContainerConnectionDetails> connectionDetails,
            ILog log,
            IOperationContext operationContext,
            IEnvironmentVariables environmentVariables,
            IImageProvider imageProvider,
            Lazy<IWorkloadRestorationService> workloadRestorationService,
            IRemoteRestoreJobDeployer remoteRestoreJobDeployer,
            IProgress<ProgressUpdate> progress,
            KubernetesPortForwardManager.Factory portForwardManagerFactory,
            DevHostAgentExecutorClient.Factory devHostAgentExecutorClientFactory)
        {
            this._kubernetesClient = kubernetesClient;
            this._remoteContainerConnectionDetails = connectionDetails;
            this._log = log;
            this._operationContext = operationContext;
            this._environmentVariables = environmentVariables;
            this._imageProvider = imageProvider;
            this._progress = progress;
            this._devHostAgentExecutorClientFactory = devHostAgentExecutorClientFactory;
            this._portForwardManager = portForwardManagerFactory(kubernetesClient);
            this._workloadRestorationService = workloadRestorationService;
            this._remoteRestoreJobDeployer = remoteRestoreJobDeployer;
        }

        /// <summary>
        /// <see cref="IRemoteEnvironmentManager.StartRemoteAgentAsync"/>
        /// </summary>
        public async Task<RemoteAgentInfo> StartRemoteAgentAsync(
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.KubernetesRemoteEnvironmentManager.AreaName, Events.KubernetesRemoteEnvironmentManager.Operations.StartRemoteAgent))
            {
                // Reset the previous context, to ensure RestoreAsync does not pick up patches in previous launches.
                // TODO do we really want this??
                this._patchState.Clear();
                var remoteContainerConnectionDetails = await _remoteContainerConnectionDetails;

                // Deploy Remote Agent and get pod and container running it
                (V1Pod pod, V1Container container) podAndContainer = (null, null);
                switch (remoteContainerConnectionDetails.AgentHostingMode)
                {
                    // New pod with agent image
                    case RemoteAgentHostingMode.NewPod:
                        podAndContainer = await this._CreateNewPodAsync(remoteContainerConnectionDetails.NamespaceName, cancellationToken);
                        break;
                    // Clone pod spec and replace container with agent
                    case RemoteAgentHostingMode.NewPodWithContext:
                        podAndContainer = await this._ClonePodAsync(remoteContainerConnectionDetails, cancellationToken);
                        break;
                    // Replace existing deployment/pod to host agent in the selected container
                    case RemoteAgentHostingMode.Replace:
                        switch (remoteContainerConnectionDetails.SourceEntityType)
                        {
                            case KubernetesEntityType.Deployment:
                                podAndContainer = await this._PatchDeploymentAsync(remoteContainerConnectionDetails, cancellationToken);
                                break;

                            case KubernetesEntityType.StatefulSet:
                                podAndContainer = await this._PatchStatefulSetAsync(remoteContainerConnectionDetails, cancellationToken);
                                break;

                            case KubernetesEntityType.Pod:
                                podAndContainer = await this._ClonePodAsync(remoteContainerConnectionDetails, cancellationToken);
                                break;

                            default:
                                throw new InvalidOperationException($"Unsupported source entity type: {remoteContainerConnectionDetails.SourceEntityType}");
                        }
                        break;
                };

                if (podAndContainer.pod == null || podAndContainer.container == null)
                {
                    throw new UserVisibleException(_operationContext, Resources.FailedToFindPodFormat, remoteContainerConnectionDetails.PodName, remoteContainerConnectionDetails.ContainerName);
                }

                _log.Verbose("Deployed remote agent to container '{0}' in pod '{1}'.", new PII(podAndContainer.container.Name), new PII(podAndContainer.pod.Metadata.Name));
                this._ReportProgress(Resources.RemoteAgentDeployedInPod, podAndContainer.container.Name, podAndContainer.pod.Metadata.Name);

                // Persist Remote Agent pod info
                this._remoteAgentPod = podAndContainer.pod;

                // Save remote agent info
                var remoteAgentInfo = new RemoteAgentInfo
                {
                    NamespaceName = remoteContainerConnectionDetails.NamespaceName,
                    PodName = podAndContainer.pod.Metadata.Name,
                    ContainerName = podAndContainer.container.Name
                };

                perfLogger.SetSucceeded();

                return remoteAgentInfo;
            }
        }

        /// <summary>
        /// <see cref="IRemoteEnvironmentManager.ConnectToRemoteAgentAsync"/>
        /// </summary>
        public async Task<int> ConnectToRemoteAgentAsync(RemoteAgentInfo remoteAgentInfo, CancellationToken cancellationToken)
        {
            // If an existing devhostagent client exists, dispose it.
            if (_devHostAgentExecutorClient != null)
            {
                _devHostAgenPortForwardCancellationTokenSource?.Cancel();
                _devHostAgentExecutorClient.Dispose();

                _devHostAgentLocalPort = 0;
                _devHostAgenPortForwardCancellationTokenSource = new CancellationTokenSource();
            }

            _log.Verbose($"Preparing to connect to the remote agent running in {remoteAgentInfo.NamespaceName}/{remoteAgentInfo.PodName}...");
            this._ReportProgress(Resources.PreparingToRunBridgeToKubernetesFormat, Product.Name, remoteAgentInfo.NamespaceName, remoteAgentInfo.PodName);

            // Find a free port to start portforwarding for devhostagent
            _devHostAgentLocalPort = PortManagementUtilities.GetAvailableLocalPort();
            _log.Verbose("devhost agent local port:", _devHostAgentLocalPort);
            _devHostAgentExecutorClient = _devHostAgentExecutorClientFactory(_devHostAgentLocalPort);

            _portForwardManager.StartContainerPortForward(
                 remoteAgentInfo.NamespaceName,
                 remoteAgentInfo.PodName,
                 localPort: _devHostAgentLocalPort,
                 remotePort: DevHostConstants.DevHostAgent.Port,
                 onSuccessfulPortForward: null,
                 cancellationToken: _devHostAgenPortForwardCancellationTokenSource.Token);

            int retryMax = 15;
            for (int retry = 0; retry < retryMax; retry++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await _devHostAgentExecutorClient.PingAsync(timeoutMs: 500, retry: 10, cancellationToken);
                    await _devHostAgentExecutorClient.ResetAsync(cancellationToken: cancellationToken);
                    break;
                }
                catch (Exception ex)
                {
                    if (retry == retryMax - 1)
                    {
                        _log.Exception(ex);
                        throw;
                    }
                    _log.ExceptionAsWarning(ex);
                }
            }
            return _devHostAgentLocalPort;
        }

        /// <summary>
        /// <see cref="IRemoteEnvironmentManager.WaitForWorkloadStoppedAsync"/>
        /// </summary>
        public async Task<bool> WaitForWorkloadStoppedAsync(CancellationToken cancellationToken)
        {
            using (var cts = new CancellationTokenSource())
            using (cancellationToken.Register(() => cts.Cancel()))
            using (var perfLogger = _log.StartPerformanceLogger(
                Events.KubernetesRemoteEnvironmentManager.AreaName,
                Events.KubernetesRemoteEnvironmentManager.Operations.WaitForWorkloadStopped))
            {
                List<Task<bool>> tasks = new List<Task<bool>>();
                tasks.Add(this._WaitForPodStoppedAsync(_remoteAgentPod, cts.Token));

                var deployment = (await _remoteContainerConnectionDetails).Deployment;

                // If we patched a deployment we wait for the deployment to change before returning, if the pod dies but the deployment doesn't change the deployment will automatically re-deploy the Remote Agent pod.
                // TODO: This code needs further investigation

                if (deployment != null)
                {
                    tasks.Add(this._WaitForDeploymentChangedAsync(deployment.Metadata.NamespaceProperty, deployment.Metadata.Name, cts.Token));
                }

                try
                {
                    var t = await tasks.WhenAnyWithErrorPropagation();
                    perfLogger.SetSucceeded();
                    return t.Result;
                }
                finally
                {
                    cts.Cancel();
                }
            }
        }

        /// <summary>
        /// <see cref="IRemoteEnvironmentManager.RestoreWorkloadAsync"/>
        /// </summary>
        public async Task RestoreWorkloadAsync(CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                Events.KubernetesRemoteEnvironmentManager.AreaName,
                Events.KubernetesRemoteEnvironmentManager.Operations.RestoreRemoteAgent))
            {
                try
                {
                    await _RestoreAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _log.ExceptionAsWarning(ex);
                    var remoteContainerConnectionDetails = await _remoteContainerConnectionDetails;
                    this._ReportProgress(Resources.FailedToRestoreServiceFormat, remoteContainerConnectionDetails.NamespaceName, remoteContainerConnectionDetails.PodName, remoteContainerConnectionDetails.ContainerName);
                }
                perfLogger.SetSucceeded();
            }
        }

        /// <summary>
        /// <see cref="IRemoteEnvironmentManager.GetRemoteAgentLocalPort"/>
        /// </summary>
        /// <returns></returns>
        public int GetRemoteAgentLocalPort()
        {
            return this._devHostAgentExecutorClient.LocalPort;
        }

        #region Private members

        /// <summary>
        /// Progress reporter for <see cref="KubernetesRemoteEnvironmentManager"/>
        /// </summary>
        private void _ReportProgress(string message, params object[] args)
        {
            _progress.Report(new ProgressUpdate(0, ProgressStatus.KubernetesRemoteEnvironmentManager, new ProgressMessage(EventLevel.Informational, _log.SaferFormat(message, args))));
        }

        /// <summary>
        /// Progress reporter for <see cref="KubernetesRemoteEnvironmentManager"/>
        /// </summary>
        private void _ReportProgress(EventLevel eventLevel, string message, params object[] args)
        {
            _progress.Report(new ProgressUpdate(0, ProgressStatus.KubernetesRemoteEnvironmentManager, new ProgressMessage(eventLevel, _log.SaferFormat(message, args))));
        }

        /// <summary>
        /// Create new pod hosting the devhostagent container
        /// </summary>
        private async Task<(V1Pod pod, V1Container container)> _CreateNewPodAsync(string namespaceName, CancellationToken cancellationToken)
        {
            // User didn't specify a pod. Need to deploy a new one.
            string podName = $"{RemoteEnvironmentUtilities.SanitizedUserName()}-connect-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToLowerInvariant();
            string containerName = "lpk-agent";
            var pod = await _DeployAgentOnlyPodAsync(_imageProvider.DevHostImage, namespaceName, podName, containerName, null, null, cancellationToken);
            var container = pod.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, containerName)).First();
            return (pod, container);
        }

        /// <summary>
        /// Deploy a new Pod running devhostAgent from an empty pod spec with provided labels
        /// </summary>
        private async Task<V1Pod> _DeployAgentOnlyPodAsync(string agentImage, string namespaceName, string podName, string containerName, IDictionary<int, string> tcpPorts, IDictionary<string, string> labels, CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.KubernetesRemoteEnvironmentManager.AreaName, Events.KubernetesRemoteEnvironmentManager.Operations.DeployAgentOnlyPod))
            {
                // Generate pod spec
                V1Pod podSpec = new V1Pod()
                {
                    ApiVersion = "v1",
                    Kind = "Pod",
                    Metadata = new V1ObjectMeta
                    {
                        Name = podName
                    },
                    Spec = new V1PodSpec
                    {
                        TerminationGracePeriodSeconds = 0,
                        Containers = new List<V1Container>(),
                        NodeSelector = new Dictionary<string, string>() { { KubernetesConstants.Labels.OS, KubernetesConstants.Labels.Values.Linux } }
                    }
                };

                // Add devhostagent container to the pod
                V1Container containerSpec = new V1Container
                {
                    Image = agentImage,
                    ImagePullPolicy = "IfNotPresent",
                    Name = containerName
                };

                if (tcpPorts != null && tcpPorts.Count > 0)
                {
                    containerSpec.Ports = new List<V1ContainerPort>();
                    foreach (var p in tcpPorts)
                    {
                        containerSpec.Ports.Add(new V1ContainerPort()
                        {
                            ContainerPort = p.Key,
                            Name = p.Value,
                            Protocol = "TCP"
                        });
                    }
                }
                podSpec.Spec.Containers.Add(containerSpec);

                // Add a label to identify that the pod has been created for connect clone scenario
                if (labels != null && labels.Count > 0)
                {
                    podSpec.Metadata.Labels = labels;
                }
                if (podSpec.Metadata.Labels == null)
                {
                    podSpec.Metadata.Labels = new Dictionary<string, string>();
                }
                podSpec.Metadata.Labels[Common.Constants.Labels.ConnectCloneLabel] = "true";

                // Deploy the pod in the namespace and wait for it to get created
                var deployedPod = await _kubernetesClient.CreateV1PodAsync(namespaceName, podSpec, cancellationToken);
                if (deployedPod == null)
                {
                    throw new UserVisibleException(_operationContext, Resources.FailedToDeployPodMessage);
                }
                var podDeployment = new PodDeployment(deployedPod);
                try
                {
                    // Deploy remote restore job
                    await _remoteRestoreJobDeployer.CreateRemoteRestoreJobAsync(deployedPod.Name(), namespaceName, podDeployment, cancellationToken);
                    // Save the pod to deployed pods context
                    _patchState.AddPodDeployment(podDeployment);
                }
                catch (Exception ex)
                {
                    // Try to clean up deployed pod
                    this._ReportProgress(EventLevel.Warning, Resources.RestoringPodDeploymentMessage);
                    await _workloadRestorationService.Value.RemovePodDeploymentAsync(
                        podDeployment,
                        cancellationToken,
                        progressCallback: p => this._ReportProgress(p.Message, p.Level),
                        noThrow: true);
                    var errorMessage = $"Failed to deploy remote restore job for pod deployment {deployedPod.Namespace()}/{deployedPod.Name()}. {ex.Message}";

                    // In this case we cannot really do anything so logging it as warning
                    if (this.IsForbiddenHttpOperationException(ex))
                    {
                        _log.Warning(errorMessage);
                    }
                    else
                    {
                        _log.Error(errorMessage);
                    }
                    throw new UserVisibleException(_operationContext, ex, Resources.FailedToDeployRemoteRestoreJobFormat, deployedPod.Namespace(), deployedPod.Name(), ex.Message);
                }
                deployedPod = await _WaitForPodToRunAsync(deployedPod, TimeSpan.FromMinutes(5), cancellationToken);

                this._ReportProgress(Resources.PodCreatedFormat, deployedPod.Metadata.NamespaceProperty, deployedPod.Metadata.Name);
                perfLogger.SetSucceeded();
                return deployedPod;
            }
        }

        /// <summary>
        /// Clone a pod spec and replace the container image with devhostagent
        /// </summary>
        private V1Pod _GetClonedPodSpec(V1Pod pod, string containerName, string agentImage, bool isRoutingSession)
        {
            var newPodName = isRoutingSession ? $"{RemoteEnvironmentUtilities.SanitizedUserName()}-{pod.Metadata.Name}" : pod.Metadata.Name;
            if (newPodName.Length > 253)
            {
                newPodName = newPodName.Substring(0, 253);
            }

            // New pod spec
            V1Pod podSpec = new V1Pod()
            {
                ApiVersion = "v1",
                Kind = "Pod",
                Metadata = new V1ObjectMeta
                {
                    Name = newPodName,
                    NamespaceProperty = pod.Metadata.NamespaceProperty,
                    // Copy pod identity label.
                    // Do not copy auto generated labels like pod-metadata-hash or label selectors that might be used by a service object
                    Labels = pod.Metadata.Labels?.Where(
                        label => label.Key.Equals(KubernetesConstants.Labels.AadPodBinding)).ToDictionary(kv => kv.Key, kv => kv.Value)
                },
                Spec = pod.Spec
            };
            // Making sure that we don't copy the nodeName field, as scheduling the remote agent on the same node may not be possible if it is low on resources.
            podSpec.Spec.NodeName = null;
            podSpec.Spec.TerminationGracePeriodSeconds = 0;
            podSpec.Spec.ShareProcessNamespace = false;
            podSpec.Spec.NodeSelector = new Dictionary<string, string>() { { KubernetesConstants.Labels.OS, KubernetesConstants.Labels.Values.Linux } };

            // Add annotation to be used by Routing manager to keep track of the CorrelationIds of the pods currently being routed.
            // This is used to track client sessions from the RoutingManagers logs.
            if (podSpec.Metadata.Annotations == null)
            {
                podSpec.Metadata.Annotations = new Dictionary<string, string>();
            }
            podSpec.Metadata.Annotations[Annotations.CorrelationId] = _operationContext.CorrelationId;

            // Replace the container's image with devhostagent image
            foreach (var c in podSpec.Spec.Containers)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(c.Name, containerName))
                {
                    c.ImagePullPolicy = "IfNotPresent";
                    c.Image = agentImage;
                    c.Command = new List<string>();
                    c.Args = new List<string>();
                    var newEnv = new List<V1EnvVar>();
                    if (c.Env != null)
                    {
                        newEnv.AddRange(c.Env);
                    }

                    newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.CollectTelemetry, _environmentVariables.CollectTelemetry.ToString()));
                    newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.ConsoleVerbosity, LoggingVerbosity.Verbose.ToString()));
                    newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.CorrelationId, _operationContext.CorrelationId));

                    c.Env = newEnv;
                }
                else
                {
                    c.LivenessProbe = null;
                    c.ReadinessProbe = null;
                }
            }

            // Filter out the following that could cause Pod creation to fail.
            //  - init container: devspaces-proxy-init
            //  - container devspaces-proxy
            //  - any volume that references aks-sp and default-token*
            if (podSpec.Spec.InitContainers != null && podSpec.Spec.InitContainers.Count > 0)
            {
                podSpec.Spec.InitContainers = podSpec.Spec.InitContainers.Where(c => c.Name != "devspaces-proxy-init" && !c.Name.Contains("istio")).ToList();
            }
            if (podSpec.Spec.Containers != null && podSpec.Spec.Containers.Count > 0)
            {
                podSpec.Spec.Containers = podSpec.Spec.Containers.Where(c => c.Name != "devspaces-proxy" && c.Name != "istio-proxy").ToList();
            }
            if (podSpec.Spec.Volumes != null && podSpec.Spec.Volumes.Count > 0)
            {
                podSpec.Spec.Volumes = podSpec.Spec.Volumes.Where(v => !v.Name.Contains("istio")).ToList();
            }

            return podSpec;
        }

        /// <summary>
        /// Query the pod and wait for at least one container to reach running state
        /// </summary>
        private async Task<V1Pod> _WaitForPodToRunAsync(V1Pod pod, TimeSpan maxWaitTime, CancellationToken cancellationToken)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            this._ReportProgress(Resources.WaitingForPodFormat, pod.Metadata.Name, pod.Metadata.NamespaceProperty);
            _log.Verbose("Waiting for {0}/{1} to run.", new PII(pod.Metadata.NamespaceProperty), new PII(pod.Metadata.Name));
            while (!cancellationToken.IsCancellationRequested || stopWatch.Elapsed < maxWaitTime)
            {
                var p = await _kubernetesClient.GetV1PodAsync(pod.Metadata.NamespaceProperty, pod.Metadata.Name, cancellationToken: cancellationToken);
                if (p != null && p.Status != null && p.Status.ContainerStatuses != null && StringComparer.OrdinalIgnoreCase.Equals(p.Status.Phase, "running"))
                {
                    _log.Verbose("{0}/{1} is running.", new PII(pod.Metadata.NamespaceProperty), new PII(pod.Metadata.Name));
                    return p;
                }
                await Task.Delay(500);
            }
            throw new InvalidOperationException(string.Format(Resources.TimedOutWaitingForPodFormat, pod.Metadata.Name, pod.Metadata.NamespaceProperty));
        }

        /// <summary>
        /// Clone a pod
        /// </summary>
        private async Task<(V1Pod pod, V1Container container)> _ClonePodAsync(
            RemoteContainerConnectionDetails remoteContainerConnectionDetails,
            CancellationToken cancellationToken)
        {
            // If we are cloning because of routing and the pod has istio we should fail and let the user know that we do not support this scenario
            var isRoutingSession = !string.IsNullOrEmpty(remoteContainerConnectionDetails.RoutingHeaderValue);
            if (isRoutingSession)
            {
                if ((remoteContainerConnectionDetails.Pod?.Spec?.InitContainers != null &&
                     remoteContainerConnectionDetails.Pod.Spec.InitContainers.Where(c => c.Name.Contains("istio")).Any()) ||
                    (remoteContainerConnectionDetails.Pod?.Spec?.Containers != null &&
                     remoteContainerConnectionDetails.Pod.Spec.Containers.Where(c => c.Name.Contains("istio")).Any()))
                {
                    throw new UserVisibleException(this._operationContext, Resources.IsolationNotSupportedWithIstio);
                }
            }

            V1Pod clonedPod = _GetClonedPodSpec(remoteContainerConnectionDetails.Pod, remoteContainerConnectionDetails.ContainerName, _imageProvider.DevHostImage, isRoutingSession: isRoutingSession);
            V1Pod userPodToRestore = null;

            // If routing option is selected, add a routing label and annotation to the new pod.
            if (isRoutingSession)
            {
                var routingHeaderValue = remoteContainerConnectionDetails.RoutingHeaderValue;
                var sourceServiceName = remoteContainerConnectionDetails.ServiceName;

                if (string.IsNullOrEmpty(sourceServiceName))
                {
                    throw new UserVisibleException(_operationContext, Resources.ServiceNameNotDefinedForRoutingFormat, remoteContainerConnectionDetails.PodName, remoteContainerConnectionDetails.NamespaceName);
                }

                // Set routing label value to the name of the source service.
                if (clonedPod.Metadata.Labels == null)
                {
                    clonedPod.Metadata.Labels = new Dictionary<string, string>();
                }

                clonedPod.Metadata.Labels[Routing.RouteFromLabelName] = sourceServiceName;

                // Set routing annotation value to the routing header value provided by user.
                if (clonedPod.Metadata.Annotations == null)
                {
                    clonedPod.Metadata.Annotations = new Dictionary<string, string>();
                }
                clonedPod.Metadata.Annotations[Routing.RouteOnHeaderAnnotationName] = $"{Routing.KubernetesRouteAsHeaderName}={routingHeaderValue}";
                clonedPod.Metadata.Annotations[Routing.DebuggedContainerNameAnnotationName] = remoteContainerConnectionDetails.ContainerName;

                if (remoteContainerConnectionDetails.RoutingManagerFeatureFlags != null && remoteContainerConnectionDetails.RoutingManagerFeatureFlags.Any())
                {
                    clonedPod.Metadata.Annotations[Routing.FeatureFlagsAnnotationName] = string.Join(',', remoteContainerConnectionDetails.RoutingManagerFeatureFlags);
                }

                _log.Verbose($"Successfully set routing label and annotation on devhost agent pod.");
                this._ReportProgress(Resources.RoutingSuccessfullyEnabledFormat, clonedPod.Metadata.Name, clonedPod.Metadata.NamespaceProperty);
            }
            else
            {
                // We want to save the user pod spec to restore it later. The spec saved in the remoteContainerConnectionDetails was edited in _GetClonedPodSpec, so we resolve a fresh copy.
                userPodToRestore = await this._kubernetesClient.GetV1PodAsync(remoteContainerConnectionDetails.Pod.Metadata.NamespaceProperty, remoteContainerConnectionDetails.Pod.Metadata.Name, cancellationToken);
            }

            // Delete existing pod (if any) and wait for it to be removed
            var isPodDeleted = await WebUtilities.RetryUntilTimeWithWaitAsync(async (t) =>
            {
                try
                {
                    await _kubernetesClient.DeleteV1PodAsync(clonedPod.Namespace(), clonedPod.Name(), cancellationToken: cancellationToken);
                    return (await this._kubernetesClient.GetV1PodAsync(clonedPod.Namespace(), clonedPod.Name(), cancellationToken: cancellationToken)) == null;
                }
                catch
                {
                    return false;
                }
            },
            maxWaitTime: TimeSpan.FromMinutes(3),
            waitInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cancellationToken);

            if (!isPodDeleted)
            {
                throw new InvalidOperationException(string.Format(Resources.FailedToDeletePodFormat, clonedPod.Name()));
            }

            // Create pod and wait for it to run
            var deployedPod = await _kubernetesClient.CreateV1PodAsync(clonedPod.Metadata.NamespaceProperty, clonedPod, cancellationToken: cancellationToken);
            if (deployedPod == null)
            {
                throw new InvalidUsageException(_operationContext, Resources.FailedToDeployPodMessage);
            }
            var podDeployment = new PodDeployment(deployedPod);
            if (userPodToRestore != null)
            {
                // K8s doesn't like us to pass in an old resource version when we create a new pod. We remove it from the pod spec so we
                // don't get an error when we restore the pod later on.
                userPodToRestore.Metadata.ResourceVersion = null;
                podDeployment.UserPodToRestore = userPodToRestore;
            }

            try
            {
                // Deploy remote restore job
                await _remoteRestoreJobDeployer.CreateRemoteRestoreJobAsync(deployedPod.Name(), deployedPod.Namespace(), podDeployment, cancellationToken);
                // Save the pod to deployed pods context
                _patchState.AddPodDeployment(podDeployment);
            }
            catch (Exception ex)
            {
                // Try to clean up deployed pod
                this._ReportProgress(EventLevel.Warning, Resources.RestoringPodDeploymentMessage);
                await _workloadRestorationService.Value.RemovePodDeploymentAsync(
                    podDeployment,
                    cancellationToken,
                    progressCallback: p => this._ReportProgress(p.Message, p.Level),
                    noThrow: true);
                var errorMessage = $"Failed to deploy remote restore job for pod deployment {deployedPod.Namespace()}/{deployedPod.Name()}. {ex.Message}";

                // In this case we cannot really do anything so logging it as warning
                if (this.IsForbiddenHttpOperationException(ex))
                {
                    _log.Warning(errorMessage);
                }
                else
                {
                    _log.Error(errorMessage);
                }
                throw new UserVisibleException(_operationContext, ex, Resources.FailedToDeployRemoteRestoreJobFormat, deployedPod.Namespace(), deployedPod.Name(), ex.Message);
            }
            deployedPod = await this._WaitForPodToRunAsync(deployedPod, TimeSpan.FromMinutes(5), cancellationToken);

            this._ReportProgress(Resources.PodCreatedInNamespaceFormat, deployedPod.Metadata.Name, deployedPod.Metadata.NamespaceProperty);

            var container = deployedPod.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, remoteContainerConnectionDetails.ContainerName)).First();
            return (deployedPod, container);
        }

        private bool _IsPodTerminated(V1Pod pod)
        {
            if (pod.Status != null)
            {
                return StringComparer.OrdinalIgnoreCase.Equals(pod.Status.Phase, "Succeeded") || StringComparer.OrdinalIgnoreCase.Equals(pod.Status.Phase, "Failed");
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a container can be replaced by devhostAgent.
        /// </summary>
        private void _EnsureContainerCanBeReplacedWithAgent(V1Container containerSpec)
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(containerSpec.ImagePullPolicy, "Never"))
            {
                throw new InvalidUsageException(_operationContext, Resources.ImagePullPolicyCannotBeNeverMessage);
            }

            if (containerSpec.Command != null && containerSpec.Command.Any())
            {
                var entryPoint = containerSpec.Command[0];
                if (!ImageProvider.DevHost.SupportedEntryPoints.Contains(entryPoint, StringComparer.Ordinal))
                {
                    throw new InvalidUsageException(_operationContext, Resources.ContainerSpecCommandNotEmptyFormat, entryPoint);
                }
            }
        }

        /// <summary>
        /// Patch the deployment to get a pod running remote agent
        /// </summary>
        private async Task<(V1Pod pod, V1Container container)> _PatchDeploymentAsync(
            RemoteContainerConnectionDetails remoteContainerConnectionDetails,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.KubernetesRemoteEnvironmentManager.AreaName, Events.KubernetesRemoteEnvironmentManager.Operations.PatchDeployment))
            {
                var namespaceName = remoteContainerConnectionDetails.NamespaceName;
                var deploymentName = remoteContainerConnectionDetails.DeploymentName;
                // Get pods for the deployment, if there is no patch required we use the running pod under existing deployment
                var existingPods = (await _kubernetesClient.ListPodsForDeploymentAsync(namespaceName, deploymentName, cancellationToken))?.Items;
                if (existingPods == null)
                {
                    throw new InvalidOperationException(string.Format(Resources.FailedToListPodsFormat, namespaceName));
                }

                // Patch is null if there is no change in the deployment spec
                var (patch, reversePatch) = this._GetDeploymentJsonPatch(remoteContainerConnectionDetails.Deployment, remoteContainerConnectionDetails.Container, _imageProvider.DevHostImage);
                V1Pod result = null;
                if (patch != null)
                {
                    try
                    {
                        var settings = new Newtonsoft.Json.JsonSerializerSettings 
                        { 
                            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver { NamingStrategy = new Newtonsoft.Json.Serialization.CamelCaseNamingStrategy() }
                        };
                        await _kubernetesClient.PatchV1DeploymentAsync(namespaceName, deploymentName, new V1Patch(Newtonsoft.Json.JsonConvert.SerializeObject(patch, settings), PatchType.JsonPatch), cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        var serializedPatch = StringManipulation.RemovePrivateKeyIfNeeded(patch.Serialize());
                            
                        _log.Error($"Patch deployment {namespaceName}/{remoteContainerConnectionDetails.DeploymentName} failed. Patch is {serializedPatch}, {ex.Message}");
                        throw new UserVisibleException(_operationContext, ex, Resources.PatchResourceFailedFormat, KubernetesResourceType.Deployment.ToString(), namespaceName, deploymentName, serializedPatch, ex.Message);
                    }
                    var deploymentPatch = new DeploymentPatch(remoteContainerConnectionDetails.Deployment, reversePatch);
                    try
                    {
                        // Deploy remote restore job
                        await _remoteRestoreJobDeployer.CreateRemoteRestoreJobAsync(deploymentName, namespaceName, deploymentPatch, cancellationToken);
                        _patchState.AddDeploymentPatch(deploymentPatch);
                    }
                    catch (Exception ex)
                    {
                        // Try to clean up patched Deployment
                        this._ReportProgress(EventLevel.Warning, Resources.RestoringResourcePatchMessage, KubernetesResourceType.Deployment.ToString());
                        await _workloadRestorationService.Value.RestoreDeploymentPatchAsync(
                            deploymentPatch,
                            cancellationToken,
                            progressCallback: p => this._ReportProgress(p.Message, p.Level),
                            noThrow: true);
                        _log.Error($"Failed to deploy remote restore job for deployment {namespaceName}/{deploymentName}. {ex.Message}");
                        throw new UserVisibleException(_operationContext, ex, Resources.FailedToDeployRemoteRestoreJobFormat, namespaceName, deploymentName, ex.Message);
                    }

                    // Get new pod running under the deployment
                    V1Pod newPod = null;
                    while (true)
                    {
                        await Task.Delay(200);
                        var pods = await _kubernetesClient.ListPodsForDeploymentAsync(namespaceName, deploymentName, cancellationToken);
                        newPod = pods.Items.Where(p => existingPods.All(p1 => p1.Metadata.CreationTimestamp < p.Metadata.CreationTimestamp)).FirstOrDefault();
                        if (newPod != null)
                        {
                            break;
                        }
                    }
                    result = await _WaitForPodToRunAsync(newPod, maxWaitTime: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                    this._ReportProgress(Resources.ResourcePatchedForAgentFormat, KubernetesResourceType.Deployment.ToString(), namespaceName, deploymentName);
                }
                else
                {
                    // If there's nothing to patch, it may mean we crashed during the last run.
                    // Try to get an existing patch state from the cluster.
                    var existingPatch = await _remoteRestoreJobDeployer.TryGetExistingPatchInfoAsync<DeploymentPatch>(deploymentName, namespaceName, cancellationToken);
                    if (existingPatch != null)
                    {
                        _patchState.AddDeploymentPatch(existingPatch);
                    }
                    result = existingPods.Where(p => !this._IsPodTerminated(p) && p.Status?.StartTime != null).OrderBy(p => p.Status.StartTime).LastOrDefault();
                }
                if (result != null)
                {
                    perfLogger.SetSucceeded();
                }

                if (result == null)
                {
                    throw new UserVisibleException(_operationContext, Resources.FailedToGetRunningPodInDeploymentFormat, deploymentName, namespaceName);
                }

                var container = result.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, remoteContainerConnectionDetails.ContainerName)).First();

                return (result, container);
            }
        }

        /// <summary>
        /// Patch the statefulset to get a pod running remote agent
        /// </summary>
        private async Task<(V1Pod pod, V1Container container)> _PatchStatefulSetAsync(
            RemoteContainerConnectionDetails remoteContainerConnectionDetails,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.KubernetesRemoteEnvironmentManager.AreaName, Events.KubernetesRemoteEnvironmentManager.Operations.PatchStatefulSet))
            {
                var namespaceName = remoteContainerConnectionDetails.NamespaceName;
                var statefulSetName = remoteContainerConnectionDetails.StatefulSetName;
                // Get pods for the statefulset, if there is no patch required we use the running pod under existing statefulset
                var existingPods = (await _kubernetesClient.ListPodsForStatefulSetAsync(namespaceName, statefulSetName, cancellationToken))?.Items;
                if (existingPods == null)
                {
                    throw new InvalidOperationException(string.Format(Resources.FailedToListPodsFormat, namespaceName));
                }

                // Patch is null if there is no change in the statefulset spec
                var (patch, reversePatch) = this._GetStatefulSetJsonPatch(remoteContainerConnectionDetails.StatefulSet, remoteContainerConnectionDetails.Container, _imageProvider.DevHostImage);
                V1Pod result = null;
                if (patch != null)
                {
                    try
                    {
                        await _kubernetesClient.PatchV1StatefulSetAsync(namespaceName, statefulSetName, new V1Patch(patch, PatchType.JsonPatch), cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Patch statefulSet {namespaceName}/{remoteContainerConnectionDetails.StatefulSet} failed. Patch is {patch.Serialize()}, {ex.Message}");
                        throw new UserVisibleException(_operationContext, ex, Resources.PatchResourceFailedFormat, KubernetesResourceType.StatefulSet.ToString(), namespaceName, statefulSetName, patch.Serialize(), ex.Message);
                    }
                    var statefulSetPatch = new StatefulSetPatch(remoteContainerConnectionDetails.StatefulSet, reversePatch);
                    try
                    {
                        // Deploy remote restore job
                        await _remoteRestoreJobDeployer.CreateRemoteRestoreJobAsync(statefulSetName, namespaceName, statefulSetPatch, cancellationToken);
                        _patchState.AddStatefulSetPatch(statefulSetPatch);
                    }
                    catch (Exception ex)
                    {
                        // Try to clean up patched StatefulSet
                        this._ReportProgress(EventLevel.Warning, Resources.RestoringResourcePatchMessage, KubernetesResourceType.StatefulSet.ToString());
                        await _workloadRestorationService.Value.RestoreStatefulSetPatchAsync(
                            statefulSetPatch,
                            cancellationToken,
                            progressCallback: p => this._ReportProgress(p.Message, p.Level),
                            noThrow: true);
                        _log.Error($"Failed to deploy remote restore job for statefulSet {namespaceName}/{statefulSetName}. {ex.Message}");
                        throw new UserVisibleException(_operationContext, ex, Resources.FailedToDeployRemoteRestoreJobFormat, namespaceName, statefulSetName, ex.Message);
                    }

                    // Get new pod running under the StatefulSet
                    V1Pod newPod = null;
                    while (true)
                    {
                        await Task.Delay(200);
                        var pods = await _kubernetesClient.ListPodsForStatefulSetAsync(namespaceName, statefulSetName, cancellationToken);
                        newPod = pods.Items.Where(p => existingPods.All(p1 => p1.Metadata.CreationTimestamp < p.Metadata.CreationTimestamp)).FirstOrDefault();
                        if (newPod != null)
                        {
                            break;
                        }
                    }
                    result = await _WaitForPodToRunAsync(newPod, maxWaitTime: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
                    this._ReportProgress(Resources.ResourcePatchedForAgentFormat, KubernetesResourceType.StatefulSet.ToString(), namespaceName, statefulSetName);
                }
                else
                {
                    // If there's nothing to patch, it may mean we crashed during the last run.
                    // Try to get an existing patch state from the cluster.
                    var existingPatch = await _remoteRestoreJobDeployer.TryGetExistingPatchInfoAsync<StatefulSetPatch>(statefulSetName, namespaceName, cancellationToken);
                    if (existingPatch != null)
                    {
                        _patchState.AddStatefulSetPatch(existingPatch);
                    }
                    result = existingPods.Where(p => !this._IsPodTerminated(p) && p.Status?.StartTime != null).OrderBy(p => p.Status.StartTime).LastOrDefault();
                }
                if (result != null)
                {
                    perfLogger.SetSucceeded();
                }

                if (result == null)
                {
                    throw new UserVisibleException(_operationContext, Resources.FailedToGetRunningPodInDeploymentFormat, statefulSetName, namespaceName);
                }

                var container = result.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, remoteContainerConnectionDetails.ContainerName)).First();

                return (result, container);
            }
        }

        /// <summary>
        /// Watch the pod and wait for it to be modifed or deleted
        /// </summary>
        private async Task<bool> _WaitForPodStoppedAsync(V1Pod pod, CancellationToken cancellationToken)
        {
            var namespaceName = pod.Metadata.NamespaceProperty;
            var podName = pod.Metadata.Name;
            while (!cancellationToken.IsCancellationRequested)
            {
                int errorRetry = 5;
                TaskCompletionSource<bool> tsc = new TaskCompletionSource<bool>();
                bool podStopped = false;
                using (var podWatcher = (await _kubernetesClient.WatchV1PodAsync(
                    namespaceName,
                    podName,
                    timeoutSeconds: 60,
                    cancellationToken: cancellationToken)).Watch<V1Pod, V1PodList>((type, item) => { }))
                {
                    podWatcher.OnClosed += () =>
                    {
                        tsc.TrySetResult(false);
                    };
                    podWatcher.OnError += (ex) =>
                    {
                        errorRetry--;
                        _log.Warning($"Watching '{namespaceName}/{podName}' failed, error {(ex != null ? ex.Message : "null")}.");
                        if (errorRetry <= 0)
                        {
                            podStopped = true;
                        }
                        tsc.TrySetResult(false);
                    };
                    podWatcher.OnEvent += (watchEventType, d) =>
                    {
                        if (watchEventType == WatchEventType.Deleted)
                        {
                            _log.Info($"Pod '{namespaceName}/{podName}' is deleted.");
                            podStopped = true;
                            tsc.TrySetResult(true);
                        }
                        else if (watchEventType == WatchEventType.Modified)
                        {
                            _log.Warning($"Pod '{namespaceName}/{podName}' was modified.");
                            var pod = this._kubernetesClient.GetV1PodAsync(namespaceName, podName, cancellationToken).Result;
                            if (pod == null || this._IsPodTerminated(pod))
                            {
                                _log.Info($"Pod '{namespaceName}/{podName}' terminated.");
                                podStopped = true;
                                tsc.TrySetResult(true);
                            }
                        }
                    };
                    await tsc.Task;
                    if (podStopped)
                    {
                        while (podStopped)
                        {
                            // Wait some stabalization period to ensure no more changes
                            podStopped = false;
                            await Task.Delay(2000, cancellationToken);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Watch the deployment and wait for a modified or deleted event
        /// </summary>
        private async Task<bool> _WaitForDeploymentChangedAsync(string namespaceName, string deploymentName, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int errorRetry = 5;
                int modifiedRetry = 3;
                TaskCompletionSource<bool> tsc = new TaskCompletionSource<bool>();
                using (var deploymentWatcher = (await _kubernetesClient.WatchV1DeploymentAsync(
                    namespaceName, deploymentName, timeoutSeconds: 60, cancellationToken: cancellationToken)).Watch<V1Deployment, V1DeploymentList>((type, item) => { }))
                {
                    bool deploymentUpdated = false;
                    deploymentWatcher.OnClosed += () =>
                    {
                        tsc.TrySetResult(false);
                    };
                    deploymentWatcher.OnError += (ex) =>
                    {
                        errorRetry--;
                        _log.Warning($"Watching '{namespaceName}/{deploymentName}' failed, error {(ex != null ? ex.Message : "null")}.");
                        if (errorRetry <= 0)
                        {
                            deploymentUpdated = true;
                        }
                        tsc.TrySetResult(false);
                    };
                    deploymentWatcher.OnEvent += (watchEventType, d) =>
                    {
                        if (watchEventType == WatchEventType.Modified)
                        {
                            modifiedRetry--;
                            _log.Warning($"Watching '{namespaceName}/{deploymentName}' - deployment was updated.");
                            if (modifiedRetry <= 0)
                            {
                                deploymentUpdated = true;
                            }
                            tsc.TrySetResult(false);
                        }
                        else if (watchEventType == WatchEventType.Deleted)
                        {
                            deploymentUpdated = true;
                            tsc.TrySetResult(true);
                        }
                    };
                    await tsc.Task;
                    if (deploymentUpdated)
                    {
                        while (deploymentUpdated)
                        {
                            // Wait some stabalization period to ensure no more changes
                            deploymentUpdated = false;
                            await Task.Delay(2000, cancellationToken);
                        }
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Returns a modified deployment spec by replacing the container image with devhostagent and other changes.
        /// If there are no changes to the original spec, returns null.
        /// </summary>
        private (JsonPatchDocument<V1Deployment> patch, JsonPatchDocument<V1Deployment> reversePatch) _GetDeploymentJsonPatch(V1Deployment deployment, V1Container container, string agentImage)
        {
            bool dirty = false;
            var patch = new JsonPatchDocument<V1Deployment>();
            var reversePatch = new JsonPatchDocument<V1Deployment>();
            patch.ContractResolver = new STJCamelCaseContractResolver();
            reversePatch.ContractResolver = new STJCamelCaseContractResolver();
            int containerIndex = deployment.Spec.Template.Spec.Containers.ToList().FindIndex(c => c.Name == container.Name);

            if (deployment.Spec.Replicas != null && deployment.Spec.Replicas.Value != 1)
            {
                // update deployment to single replica
                patch.Replace(d => d.Spec.Replicas, 1);
                reversePatch.Replace(d => d.Spec.Replicas, deployment.Spec.Replicas.Value);
                dirty = true;
            }
            if (deployment.Spec.Template.Spec.ShareProcessNamespace != null && deployment.Spec.Template.Spec.ShareProcessNamespace.Value)
            {
                // update deployment to disable process namespace sharing
                patch.Replace(d => d.Spec.Template.Spec.ShareProcessNamespace, false);
                reversePatch.Replace(d => d.Spec.Template.Spec.ShareProcessNamespace, true);
                dirty = true;
            }
            if (deployment.Spec.MinReadySeconds != null)
            {
                // remove min ready seconds so that the new pod is considered ready as soon as it is up
                patch.Replace(d => d.Spec.MinReadySeconds, null);
                reversePatch.Replace(d => d.Spec.MinReadySeconds, deployment.Spec.MinReadySeconds);
                dirty = true;
            }
            if (deployment.Spec.Template.Spec.Containers[containerIndex].Command != null)
            {
                // update deployment to remove commands
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Command, new string[0]);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Command, deployment.Spec.Template.Spec.Containers[containerIndex].Command);
                dirty = true;
            }
            if (deployment.Spec.Template.Spec.Containers[containerIndex].Args != null)
            {
                // update deployment to remove args
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Args, new string[0]);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Args, deployment.Spec.Template.Spec.Containers[containerIndex].Args);
                dirty = true;
            }
            if (deployment.Spec.Template.Spec.Containers[containerIndex].ImagePullPolicy == "Never")
            {
                // update deployment not to use ImagePullPolicy=Never
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].ImagePullPolicy, "IfNotPresent");
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].ImagePullPolicy, "Never");
                dirty = true;
            }
            if (deployment.Spec.Template.Spec.Containers[containerIndex].Image != agentImage)
            {
                // update deployment to use Image=agentImage
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Image, agentImage);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Image, deployment.Spec.Template.Spec.Containers[containerIndex].Image);
                dirty = true;
            }
            if (deployment.Spec.Template.Spec.Containers[containerIndex].Env?.Count() != 1 || StringComparer.OrdinalIgnoreCase.Compare(deployment.Spec.Template.Spec.Containers[containerIndex].Env?.First().Name, EnvironmentVariables.Names.CollectTelemetry) != 0)
            {
                // update deployment to set environment variables for devhostagent
                var newEnv = new List<V1EnvVar>();
                if (deployment.Spec.Template.Spec.Containers[containerIndex].Env != null)
                {
                    newEnv.AddRange(deployment.Spec.Template.Spec.Containers[containerIndex].Env);
                }

                newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.CollectTelemetry, _environmentVariables.CollectTelemetry.ToString()));
                newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.ConsoleVerbosity, LoggingVerbosity.Verbose.ToString()));
                newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.CorrelationId, _operationContext.CorrelationId));

                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Env, newEnv);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Env, deployment.Spec.Template.Spec.Containers[containerIndex].Env);
                dirty = true;
            }

            _log.Info($"Deployment patch created: {dirty}");
            return dirty ? (patch, reversePatch) : (null, null);
        }

        /// <summary>
        /// Returns a modified statefulset spec by replacing the container image with devhostagent and other changes.
        /// If there are no changes to the original spec, returns null.
        /// TODO: Bug 1292855: Find a way to re-use code between deployment & statefulset patching logic
        /// </summary>
        private (JsonPatchDocument<V1StatefulSet> patch, JsonPatchDocument<V1StatefulSet> reversePatch) _GetStatefulSetJsonPatch(V1StatefulSet statefulSet, V1Container container, string agentImage)
        {
            bool dirty = false;
            var patch = new JsonPatchDocument<V1StatefulSet>();
            var reversePatch = new JsonPatchDocument<V1StatefulSet>();
            patch.ContractResolver = new STJCamelCaseContractResolver();
            reversePatch.ContractResolver = new STJCamelCaseContractResolver();
            int containerIndex = statefulSet.Spec.Template.Spec.Containers.ToList().FindIndex(c => c.Name == container.Name);

            if (statefulSet.Spec.Replicas != null && statefulSet.Spec.Replicas.Value != 1)
            {
                // update statefulset to single replica
                patch.Replace(d => d.Spec.Replicas, 1);
                reversePatch.Replace(d => d.Spec.Replicas, statefulSet.Spec.Replicas.Value);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.ShareProcessNamespace != null && statefulSet.Spec.Template.Spec.ShareProcessNamespace.Value)
            {
                // update statefulset to disable process namespace sharing
                patch.Replace(d => d.Spec.Template.Spec.ShareProcessNamespace, false);
                reversePatch.Replace(d => d.Spec.Template.Spec.ShareProcessNamespace, true);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].LivenessProbe != null)
            {
                // update statefulset to disable liveness probe
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].LivenessProbe, null);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].LivenessProbe, statefulSet.Spec.Template.Spec.Containers[containerIndex].LivenessProbe);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].ReadinessProbe != null)
            {
                // update statefulset to disable readiness probe
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].ReadinessProbe, null);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].ReadinessProbe, statefulSet.Spec.Template.Spec.Containers[containerIndex].ReadinessProbe);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].Command != null)
            {
                // update statefulset to remove commands
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Command, new string[0]);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Command, statefulSet.Spec.Template.Spec.Containers[containerIndex].Command);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].Args != null)
            {
                // update statefulset to remove args
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Args, new string[0]);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Args, statefulSet.Spec.Template.Spec.Containers[containerIndex].Args);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].ImagePullPolicy == "Never")
            {
                // update statefulset not to use ImagePullPolicy=Never
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].ImagePullPolicy, "IfNotPresent");
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].ImagePullPolicy, "Never");
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].Image != agentImage)
            {
                // update statefulset to use Image=agentImage
                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Image, agentImage);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Image, statefulSet.Spec.Template.Spec.Containers[containerIndex].Image);
                dirty = true;
            }
            if (statefulSet.Spec.Template.Spec.Containers[containerIndex].Env?.Count() != 1 || StringComparer.OrdinalIgnoreCase.Compare(statefulSet.Spec.Template.Spec.Containers[containerIndex].Env?.First().Name, EnvironmentVariables.Names.CollectTelemetry) != 0)
            {
                // update statefulset to set environment variables for devhostagent
                var newEnv = new List<V1EnvVar>();
                if (statefulSet.Spec.Template.Spec.Containers[containerIndex].Env != null)
                {
                    newEnv.AddRange(statefulSet.Spec.Template.Spec.Containers[containerIndex].Env);
                }

                newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.CollectTelemetry, _environmentVariables.CollectTelemetry.ToString()));
                newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.ConsoleVerbosity, LoggingVerbosity.Verbose.ToString()));
                newEnv.Add(new V1EnvVar(EnvironmentVariables.Names.CorrelationId, _operationContext.CorrelationId));

                patch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Env, newEnv);
                reversePatch.Replace(d => d.Spec.Template.Spec.Containers[containerIndex].Env, statefulSet.Spec.Template.Spec.Containers[containerIndex].Env);
                dirty = true;
            }

            _log.Info($"StatefulSet patch created: {dirty}");
            return dirty ? (patch, reversePatch) : (null, null);
        }

        /// <summary>
        /// Delete all new deployed pods and services and patch pods and deployments with old images
        /// </summary>
        private async Task _RestoreAsync(CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.KubernetesRemoteEnvironmentManager.AreaName, Events.KubernetesRemoteEnvironmentManager.Operations.Restore))
            {
                // Restore patch state
                var deploymentPatches = _patchState.DeploymentPatches.ToArray();
                var podPatches = _patchState.PodPatches.ToArray();
                var podDeployments = _patchState.PodDeployments.ToArray();
                var statefulsetPatches = _patchState.StatefulSetPatches.ToArray();
                void progressAction(ProgressMessage p) => this._ReportProgress(p.Message, p.Level);

                foreach (var d in deploymentPatches)
                {
                    await _workloadRestorationService.Value.RestoreDeploymentPatchAsync(d, cancellationToken, progressAction);
                    await _remoteRestoreJobDeployer.CleanupRemoteRestoreJobAsync(d.Deployment.Name(), d.Deployment.Namespace(), cancellationToken);
                    _patchState.TryRemoveDeploymentPatch(d.Deployment);
                }
                foreach (var s in statefulsetPatches)
                {
                    await _workloadRestorationService.Value.RestoreStatefulSetPatchAsync(s, cancellationToken, progressAction);
                    await _remoteRestoreJobDeployer.CleanupRemoteRestoreJobAsync(s.StatefulSet.Name(), s.StatefulSet.Namespace(), cancellationToken);
                    _patchState.TryRemoveStatefulSetPatch(s.StatefulSet);
                }
                foreach (var p in podPatches)
                {
                    await _workloadRestorationService.Value.RestorePodPatchAsync(p, cancellationToken, progressAction);
                    await _remoteRestoreJobDeployer.CleanupRemoteRestoreJobAsync(p.Pod.Name(), p.Pod.Namespace(), cancellationToken);
                    _patchState.TryRemovePodPatch(p.Pod);
                }
                foreach (var p in podDeployments)
                {
                    await _workloadRestorationService.Value.RemovePodDeploymentAsync(p, cancellationToken, progressAction);
                    await _remoteRestoreJobDeployer.CleanupRemoteRestoreJobAsync(p.Pod.Name(), p.Pod.Namespace(), cancellationToken);
                    _patchState.TryRemovePodDeployment(p.Pod);
                }

                _patchState.Clear();
                perfLogger.SetSucceeded();
            }
        }

        private bool IsForbiddenHttpOperationException(Exception ex)
        {
            if (ex is HttpOperationException httpOperationException)
            {
                var statusCode = httpOperationException?.Response?.StatusCode;
                return statusCode == System.Net.HttpStatusCode.Unauthorized || statusCode == System.Net.HttpStatusCode.Forbidden;
            }

            return false;
        }

        #endregion Private members
    }
}