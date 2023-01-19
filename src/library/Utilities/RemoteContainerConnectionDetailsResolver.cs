// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Utilities
{
    internal class RemoteContainerConnectionDetailsResolver
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly ILog _log;
        private readonly IOperationContext _operationContext;

        public delegate RemoteContainerConnectionDetailsResolver Factory(IKubernetesClient kubernetesClient);

        public RemoteContainerConnectionDetailsResolver(
            IKubernetesClient kubernetesClient,
            ILog log,
            IOperationContext operationContext)
        {
            this._kubernetesClient = kubernetesClient;
            this._log = log;
            this._operationContext = operationContext;
        }

        public async Task<RemoteContainerConnectionDetails> ResolveConnectionDetails(
            RemoteContainerConnectionDetails remoteContainerConnectionDetails,
            CancellationToken cancellationToken)
        {
            // If we are in prep there is no need to resolve anything since we are only using the remoteContainerConnectionDetails to hold a namespace
            // TODO (lolodi): this needs to be refactored so that we don't need to use such workarounds.
            if (remoteContainerConnectionDetails.AgentHostingMode == RemoteAgentHostingMode.PrepConnect)
            {
                return remoteContainerConnectionDetails;
            }

            // Check if restoration pod is present and in running state. This is an indication that previous session is still connected or it has not finished restoring yet.
            var allPods = (await _kubernetesClient.ListPodsInNamespaceAsync(remoteContainerConnectionDetails.NamespaceName, null, cancellationToken))?.Items;
            if (allPods != null && allPods.Count > 0) {
                var podsRunningRestorationJob = allPods.Where(p => StringComparer.OrdinalIgnoreCase.Equals(p.Status?.Phase, "Running") &&
                                                        p.Metadata.Name.StartsWith(remoteContainerConnectionDetails.ServiceName + "-restore") &&
                                                        (p.Status?.ContainerStatuses?.Where(cs => cs.Image.Contains(ImageProvider.DevHostRestorationJob.Name)).Any() ?? false));
                if (podsRunningRestorationJob.Count() > 0)
                {
                    _log.Warning("Restoration image {0} was found for '{1}' service. This means previous session is not restored yet. Try again once previous session is disconnected and restored.",
                    ImageProvider.DevHostRestorationJob.Name, remoteContainerConnectionDetails.ServiceName);
                    throw new UserVisibleException(_operationContext, Resources.PreviousSessionStillConnected, remoteContainerConnectionDetails.ServiceName);
                }
            }

            // Our target in remoteContainerConnectionDetails can be specified in a variety of ways. The most common one is Service.
            // If a service is specified we need to find the pod(s) that back that service and, if routing is disabled, the deployment that back the pods.
            // The deployment is not used when routing is enabled because routing deploys a new standalone pod.

            // If we are targeting a Service, we need to resolve the pod (or pods) that back the service.
            if (remoteContainerConnectionDetails.SourceEntityType == KubernetesEntityType.Service)
            {
                var service = await _kubernetesClient.GetV1ServiceAsync(
                    remoteContainerConnectionDetails.NamespaceName,
                    remoteContainerConnectionDetails.ServiceName,
                    cancellationToken);
                remoteContainerConnectionDetails.UpdateServiceDetails(service ?? throw new UserVisibleException(_operationContext, Resources.SpecifiedServiceCouldNotBeFoundFormat, remoteContainerConnectionDetails.ServiceName, remoteContainerConnectionDetails.NamespaceName));

                var pods = await this._GetTargetPodsFromServiceAsync(
                    remoteContainerConnectionDetails.NamespaceName,
                    remoteContainerConnectionDetails.Service,
                    cancellationToken);

                _log.Verbose($"Resolved {pods.Count()} from service.");

                // We want to see how many containers each pod has. Our current assumption is that all pods backing a service have the same number of containers, but if this is not the case
                // we will need to either improve how we resolve the pod, or provide a way for the user to tell us which container to use
                var minNumberContainers = pods.Select(p => p.Spec.Containers.Count).Min();
                var maxNumberContainers = pods.Select(p => p.Spec.Containers.Count).Max();
                _log.Verbose($"Max number of containers in pod: {maxNumberContainers}, min number of containers in pod: {minNumberContainers}");

                // If there are multiple pods backing the service it is probably because there is a deployment or statefulset with replicas > 1
                // To be sure we check if the pods are backed by the same deployment and while we are at it we cache the deployment in case we need it later.
                if (pods.Count() > 1)
                {
                    await ResolveBackingObjectDetailsAsync(remoteContainerConnectionDetails, pods, cancellationToken);
                }

                // We now have the pod(s). We need to update the remoteContainerConnectionDetails with it.
                // TODO: This system of setting the source entity multiple times (e.g. we start as "Service", get set to "Pod" and then later to "Deployment"/"StatefulSet")
                // is confusing and hard to follow. We should find a better way to do this while still reusing logic between the different types.
                var firstPod = pods.First();
                _log.Verbose($"Chose pod '{new PII(firstPod?.Name())}' with {firstPod.Spec.Containers.Count} containers");
                remoteContainerConnectionDetails.UpdateSourceEntityTypeToPod(firstPod);

                V1Container container;
                // If a containerName was specified let's make sure it exists
                if (!string.IsNullOrWhiteSpace(remoteContainerConnectionDetails.ContainerName))
                {
                    _log.Verbose($"Container {new PII(remoteContainerConnectionDetails.ContainerName)} was specified.");
                    container = remoteContainerConnectionDetails.Pod.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, remoteContainerConnectionDetails.ContainerName)).FirstOrDefault();
                    remoteContainerConnectionDetails.UpdateContainerDetails(container ?? throw new UserVisibleException(_operationContext, Resources.SpecifiedContainerNotFoundFormat, remoteContainerConnectionDetails.ContainerName, remoteContainerConnectionDetails.PodName));
                }
                else
                {
                    // The user did not specify a container so we need to infere it from the pod and service
                    container = (this.GetContainerFromPodAndService(remoteContainerConnectionDetails.Pod, remoteContainerConnectionDetails.Service)) ??
                                    throw new UserVisibleException(_operationContext, Resources.FailedToIDContainerFormat, remoteContainerConnectionDetails.Pod.Metadata.Name);
                }
                remoteContainerConnectionDetails.UpdateContainerDetails(container);
            }

            // If we are targeting a Deployment, we need to fetch it from K8S and make sure the container is there
            if (remoteContainerConnectionDetails.SourceEntityType == KubernetesEntityType.Deployment)
            {
                var deployment = await _kubernetesClient.GetV1DeploymentAsync(
                    remoteContainerConnectionDetails.NamespaceName,
                    remoteContainerConnectionDetails.DeploymentName,
                    cancellationToken);
                remoteContainerConnectionDetails.UpdateDeploymentDetails(deployment ?? throw new UserVisibleException(_operationContext, Resources.FailedToFindDeploymentFormat, remoteContainerConnectionDetails.DeploymentName, remoteContainerConnectionDetails.NamespaceName));

                // If a containerName was specified let's make sure it exists
                if (!string.IsNullOrWhiteSpace(remoteContainerConnectionDetails.ContainerName))
                {
                    var container = remoteContainerConnectionDetails.Deployment.Spec.Template.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, remoteContainerConnectionDetails.ContainerName)).FirstOrDefault();
                    remoteContainerConnectionDetails.UpdateContainerDetails(container ?? throw new UserVisibleException(_operationContext, Resources.FailedToFindContainerInDeploymentFormat, remoteContainerConnectionDetails.DeploymentName, remoteContainerConnectionDetails.NamespaceName));
                }
                else
                {
                    var container = this.GetContainerFromDeployment(remoteContainerConnectionDetails.Deployment);
                    remoteContainerConnectionDetails.UpdateContainerDetails(container ?? throw new UserVisibleException(_operationContext, Resources.FailedToFindContainerInDeploymentFormat, remoteContainerConnectionDetails.DeploymentName, remoteContainerConnectionDetails.NamespaceName));
                }
            }

            // If we are targeting a Pod, it might be that is beacuse we were targeting a Service, and we moved to a Pod, or because the user specified a Pod manually (only the Pod name is known)
            if (remoteContainerConnectionDetails.SourceEntityType == KubernetesEntityType.Pod)
            {
                if (remoteContainerConnectionDetails.Pod == null)
                {
                    // We have a prefix of the pod name. We try to look for the pod based on what we have.
                    _log.Verbose($"Trying to resolve pod that matches {new PII(remoteContainerConnectionDetails.PodName)}...");
                    var pods = await _kubernetesClient.ListPodsInNamespaceAsync(remoteContainerConnectionDetails.NamespaceName, cancellationToken: cancellationToken);
                    var pod = pods.Items.FirstOrDefault(p => p.Metadata.Name.StartsWith(remoteContainerConnectionDetails.PodName));
                    remoteContainerConnectionDetails.UpdatePodDetails(pod ?? throw new UserVisibleException(_operationContext, Resources.FailedToFindPodInNamespaceFormat, remoteContainerConnectionDetails.PodName, remoteContainerConnectionDetails.NamespaceName));
                }
                // We know have the full pod, or because it was resolved from the service before, or because we just resolved it above.

                if (remoteContainerConnectionDetails.Container == null)
                {
                    // If a containerName was specified let's make sure it exists
                    if (!string.IsNullOrWhiteSpace(remoteContainerConnectionDetails.ContainerName))
                    {
                        var container = remoteContainerConnectionDetails.Pod.Spec.Containers.Where(c => StringComparer.OrdinalIgnoreCase.Equals(c.Name, remoteContainerConnectionDetails.ContainerName)).FirstOrDefault();
                        remoteContainerConnectionDetails.UpdateContainerDetails(container ?? throw new UserVisibleException(_operationContext, Resources.SpecifiedContainerNotFoundFormat, remoteContainerConnectionDetails.ContainerName, remoteContainerConnectionDetails.PodName));
                    }
                    else
                    {
                        var container = this.GetContainerFromPod(remoteContainerConnectionDetails.Pod);
                        remoteContainerConnectionDetails.UpdateContainerDetails(container ?? throw new UserVisibleException(_operationContext, Resources.FailedToFindContainerInPodFormat, remoteContainerConnectionDetails.ContainerName, remoteContainerConnectionDetails.PodName));
                    }
                }
            }

            // If source entity is a Pod, and routing is OFF, try to find the deployment that is hosting the Pod
            if (remoteContainerConnectionDetails.SourceEntityType == KubernetesEntityType.Pod &&
            remoteContainerConnectionDetails.AgentHostingMode == RemoteAgentHostingMode.Replace)
            {
                try
                {
                    // We might already have a backing object here if we came in as Service and then found multiple Pods backed by the same Deployment or Statefulset
                    if (remoteContainerConnectionDetails.Deployment == null && remoteContainerConnectionDetails.StatefulSet == null)
                    {
                        await ResolveBackingObjectDetailsAsync(remoteContainerConnectionDetails, remoteContainerConnectionDetails.Pod.AsEnumerable(), cancellationToken);
                    }
                    // The container at this point should already have been resolved when we processed the Pod in the block above, so no need to re-resolve it.

                    if (remoteContainerConnectionDetails.Deployment != null)
                    {
                        remoteContainerConnectionDetails.UpdateSourceEntityTypeToDeployment(remoteContainerConnectionDetails.Deployment);
                    }
                    else if (remoteContainerConnectionDetails.StatefulSet != null)
                    {
                        remoteContainerConnectionDetails.UpdateSourceEntityTypeToStatefulSet(remoteContainerConnectionDetails.StatefulSet);
                    }
                }
                catch (Exception e)
                {
                    _log.Info($"Failed to resolve backing object for pod: {e.Message}. Proceeding to debug single pod.");
                }
            }

            return remoteContainerConnectionDetails;
        }

        #region Private Resolver Helpers

        private async Task ResolveBackingObjectDetailsAsync(
            RemoteContainerConnectionDetails remoteContainerConnectionDetails,
            IEnumerable<V1Pod> pods,
            CancellationToken cancellationToken)
        {
            (var owningSetName, var owningObjectType) = GetOwningSetFromPods(pods, remoteContainerConnectionDetails.ServiceName);
            _log.Verbose($"Owning object type: {owningObjectType}");
            if (string.Equals(owningObjectType, "ReplicaSet"))
            {
                // There is a single common replicaSet owning all the pods. Time to make sure there is a deployment owning it
                var replicaSet = await _kubernetesClient.GetV1ReplicaSetAsync(remoteContainerConnectionDetails.NamespaceName, owningSetName, cancellationToken: cancellationToken);
                if (replicaSet == null || replicaSet.Metadata?.OwnerReferences == null)
                {
                    // replicaSet not found or no deployment owning it
                    throw new InvalidOperationException(string.Format(Resources.FailedToResolveResourceBackingServiceFormat, "ReplicaSet", remoteContainerConnectionDetails.ServiceName));
                }

                var deployments = replicaSet.Metadata.OwnerReferences.Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.Kind, "Deployment")).Select(d => d.Name);
                if (deployments.Count() != 1)
                {
                    throw new InvalidOperationException(string.Format(Resources.FailedToResolveResourceBackingServiceFormat, "Deployment", remoteContainerConnectionDetails.ServiceName));
                }
                var deployment = await _kubernetesClient.GetV1DeploymentAsync(remoteContainerConnectionDetails.NamespaceName, deployments.First(), cancellationToken);
                if (deployment == null)
                {
                    throw new InvalidOperationException(string.Format(Resources.FailedToResolveResourceBackingServiceFormat, "Deployment", remoteContainerConnectionDetails.ServiceName));
                }
                _log.Verbose($"Resolved backing deployment: {new PII(deployment.Name())}");
                remoteContainerConnectionDetails.UpdateSourceEntityTypeToDeployment(deployment);
            }
            else
            {
                var statefulSet = await _kubernetesClient.GetV1StatefulSetAsync(remoteContainerConnectionDetails.NamespaceName, owningSetName, cancellationToken);
                if (statefulSet == null)
                {
                    throw new InvalidOperationException(string.Format(Resources.FailedToResolveResourceBackingServiceFormat, "StatefulSet", remoteContainerConnectionDetails.ServiceName));
                }
                _log.Verbose($"Resolved backing stateful set: {new PII(statefulSet.Name())}");
                remoteContainerConnectionDetails.UpdateSourceEntityTypeToStatefulSet(statefulSet);
            }
        }

        /// <summary>
        /// Gets the pods that back the target service that can be used as connection target (e.g. not already running DevHostAgent, passing no-Windows validation, etc)
        /// </summary>
        private async Task<IEnumerable<V1Pod>> _GetTargetPodsFromServiceAsync(
            string namespaceName,
            V1Service service,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                Events.KubernetesRemoteEnvironmentManager.AreaName,
                Events.KubernetesRemoteEnvironmentManager.Operations.GetPodsFromService))
            {
                // Try to get the service spec
                if (service?.Spec?.Selector == null || !service.Spec.Selector.Any())
                {
                    throw new UserVisibleException(_operationContext, Resources.SpecifiedServiceCouldNotBeFoundFormat, service.Metadata?.Name, namespaceName);
                }

                IDictionary<string, string> selectors;
                // Find pods with the same label selector as the service
                if (service?.Metadata?.Annotations != null && service.Metadata.Annotations.ContainsKey(Routing.OriginalServiceSelectorAnnotation))
                {
                    selectors = JsonHelpers.DeserializeObject<Dictionary<string, string>>(service.Metadata.Annotations[Routing.OriginalServiceSelectorAnnotation]);
                }
                else
                {
                    selectors = service.Spec.Selector;
                }
                var pods = (await _kubernetesClient.ListPodsInNamespaceAsync(namespaceName, selectors, cancellationToken))?.Items;
                if (pods == null || !pods.Any())
                {
                    throw new UserVisibleException(_operationContext, Resources.SpecifiedServiceNotBackedByRunningPodFormat, service.Metadata?.Name, $"kubectl get pods --namespace {namespaceName}");
                }

                if (pods.Count() != 1)
                {
                    // There can be a transition state where the previous pod running DevHostAgent has not been removed yet and the pod running the user container is running.
                    // In this case when the user tries to connect we would get both kind of pods here that back the same service. With this code we are trying to be smart and pick the pods not running the DevHostAgent.
                    var podsNotRunningDevHostAgent = pods.Where(p =>
                                                        !p.Status?.ContainerStatuses?.Where(cs => cs.Image.Contains(ImageProvider.DevHost.Name)).Any() ?? false);

                    if (podsNotRunningDevHostAgent.Count() == 0)
                    {
                        // There are multiple pods, all running devhost agent, we don't know which one we should connect to. This is a corner case that should not be happening, but better to keep an eye out just in case.
                        _log.Warning("{0} pods found, but they are all running RemoteAgent", pods.Count);
                        throw new UserVisibleException(_operationContext, Resources.SpecifiedServiceBackedByMultipleRemoteAgents, service.Metadata?.Name, pods.Count);
                    }
                    pods = podsNotRunningDevHostAgent.ToList();
                }

                // Fail fast if the target workload is Windows-based
                // If a list of pods contains even a pod targeting Windows we fail
                pods.ExecuteForEach(p => p.ThrowIfRunningOnWindowsNodes(_operationContext));

                perfLogger.SetSucceeded();
                return pods;
            }
        }

        private (string owningSetName, string owningSetType) GetOwningSetFromPods(IEnumerable<V1Pod> pods, string serviceName)
        {
            if (pods == null || !pods.Any())
            {
                throw new InvalidOperationException("Failed to determine the resource backing the service");
            }

            // All pods must be owned by the same replicaset or statefulset
            // Initialize the result with the first pod replicaSets
            var resourceType = string.Empty;
            var owningSets = pods.First().Metadata?.OwnerReferences?.Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.Kind, "ReplicaSet")).Select(r => r.Name);
            if (owningSets != null && owningSets.Any())
            {
                resourceType = "ReplicaSet";
            }
            else
            {
                // Try looking for stateful set
                owningSets = pods.First().Metadata?.OwnerReferences?.Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.Kind, "StatefulSet")).Select(r => r.Name);
                if (owningSets == null || !owningSets.Any())
                {
                    throw new UserVisibleException(_operationContext, Resources.ResourceNotSupportedFormat, Product.Name);
                }
                resourceType = "StatefulSet";
            }

            foreach (var pod in pods)
            {
                // For each pod, intersect the initial result with the set owning the current pod
                var podReplicaSets = pod.Metadata?.OwnerReferences?.Where(r => StringComparer.OrdinalIgnoreCase.Equals(r.Kind, resourceType)).Select(r => r.Name);
                if (podReplicaSets == null || !podReplicaSets.Any())
                {
                    throw new InvalidOperationException(string.Format(Resources.FailedToResolveResourceBackingServiceFormat, resourceType, serviceName));
                }
                owningSets = owningSets.Intersect(podReplicaSets, StringComparer.OrdinalIgnoreCase);

                // If at any time the intersection is 0, it means that there is no single resource is owning all the pods
                if (owningSets.Count() == 0)
                {
                    throw new UserVisibleException(_operationContext, Resources.SpecifiedServiceBackedByManyPodsInDifferentResourcesFormat, serviceName, pods.Count(), $"{resourceType}s");
                }
            }
            if (owningSets.Count() != 1)
            {
                throw new InvalidOperationException(string.Format(Resources.FailedToResolveResourceBackingServiceFormat, resourceType, serviceName));
            }

            return (owningSets.First(), resourceType);
        }

        #region Container Finders

        // There are multiple ways in which we can identify a container dependening on what info we have
        private V1Container GetContainerFromPodAndService(V1Pod pod, V1Service service)
        {
            _log.Verbose($"Pod {new PII(pod.Name())} owned by service {new PII(service.Name())} contains {pod.Spec.Containers.Count} containers.");
            V1Container sourceContainer = null;

            // If there is only one container choose that
            var containersThatAreNotProxySidecars = pod.Spec.Containers.Where(c => !c.IsKnownSidecarContainer());
            if (containersThatAreNotProxySidecars.Count() == 1)
            {
                sourceContainer = containersThatAreNotProxySidecars.First();
                _log.Verbose($"Resolved source container {new PII(sourceContainer.Name)}");
            }

            var containerWithExposedPorts = pod.Spec.Containers.Where(c => c.Ports != null).ToList();
            _log.Verbose($"Resolved {containerWithExposedPorts.Count} containers with exposed ports.");

            // If the container is not found, try to find the container with same port as the target port of the service
            if (sourceContainer == null)
            {
                var serviceTargetPorts = service.Spec.Ports?.Select(p => p.TargetPort.Value.ToString().ToLowerInvariant()) ?? new List<string>();
                _log.Verbose($"Resolved {serviceTargetPorts.Count()} target ports for the service.");
                foreach (var c in containerWithExposedPorts)
                {
                    var containerPorts = c.Ports.Select(p => p.ContainerPort.ToString().ToLowerInvariant()).ToList();
                    _log.Verbose($"Resolved {containerPorts.Count()} container ports.");
                    // Port name may not be always populated, so check for is null or empty
                    var containerPortNames = c.Ports.Where(p => !string.IsNullOrEmpty(p.Name)).Select(p => p.Name.ToLowerInvariant()).ToList();
                    _log.Verbose($"Resolved {containerPortNames.Count()} container port names.");

                    if (serviceTargetPorts.All(sp => containerPortNames.Contains(sp) || containerPorts.Contains(sp)))
                    {
                        // All the service target ports are covered by the current container, there should be only one container that covers the service ports.
                        sourceContainer = c;
                        _log.Verbose($"Resolved source container that covers all target service port(s): {new PII(sourceContainer.Name)}");
                        break;
                    }
                }
            }

            // If the container is still not found, try to find the container which exposes the same port as the service
            if (sourceContainer == null)
            {
                // Try to find the container that exposes the port mentioned in the service spec
                var servicePorts = service.Spec.Ports?.Select(p => p.Port) ?? new List<int>();
                foreach (var c in containerWithExposedPorts)
                {
                    var containerPorts = c.Ports.Select(p => p.ContainerPort).ToList();
                    if (servicePorts.All(sp => containerPorts.Contains(sp)))
                    {
                        // All the service ports are covered by the current container, there should be only one container that covers the service ports.
                        sourceContainer = c;
                        _log.Verbose($"Resolved source container that covers all service ports: {new PII(sourceContainer.Name)}");
                        break;
                    }
                }
            }

            if (sourceContainer == null)
            {
                throw new UserVisibleException(_operationContext, Resources.FailedToIDContainerFormat, pod.Metadata.Name);
            }

            // We found the Goldilocks Container, Hurray!!
            _log.Verbose($"Successfully got container from service and pod.");
            return sourceContainer;
        }

        /// <summary>
        /// Find container name from the pod
        /// </summary>
        private V1Container GetContainerFromPod(V1Pod pod)
        {
            // Fail fast if the target workload is Windows-based
            pod.ThrowIfRunningOnWindowsNodes(_operationContext);
            var containersThatAreNotProxySidecars = pod.Spec.Containers.Where(c => !c.IsKnownSidecarContainer());

            _log.Verbose($"Resolved {containersThatAreNotProxySidecars.Count()} containers for pod {new PII(pod.Name())} that are not proxy sidecars.");
            if (containersThatAreNotProxySidecars.Count() == 1)
            {
                return containersThatAreNotProxySidecars.First();
            }

            // Container name can't be determined as there are multiple containers in the pod
            throw new UserVisibleException(_operationContext, Resources.FailedToIDContainerForPod, pod.Metadata?.Name);
        }

        /// <summary>
        /// Find the container name from the deployment
        /// This method is used if the user specifies a deployment directly.
        /// </summary>
        private V1Container GetContainerFromDeployment(V1Deployment deployment)
        {
            // Fail fast if the target workload is Windows-based
            deployment.ThrowIfRunningOnWindowsNodes(_operationContext);

            var containersThatAreNotProxySidecars = deployment.Spec.Template.Spec.Containers.Where(c => !c.IsKnownSidecarContainer());
            if (containersThatAreNotProxySidecars.Count() == 1)
            {
                return containersThatAreNotProxySidecars.First();
            }

            // Container can't be determined as there are multiple containers in the deployment
            throw new UserVisibleException(_operationContext, Resources.FailedToFindContainerInDeploymentFormat, deployment.Metadata.Name, deployment.Metadata.Namespace());
        }

        #endregion Container Finders

        #endregion Private Resolver Helpers
    }
}
