// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using k8s.Models;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// This class identifies a remote container to use as target for Connect.
    /// If a DeploymentName is specified, the container is identified by its deployment
    /// If a Pod is specified, the container is identified by the Pod it is running into.
    /// </summary>
    public class RemoteContainerConnectionDetails
    {
        /// <summary>
        /// The connection mode to adopt to deploy the remote agent.
        /// </summary>
        internal RemoteAgentHostingMode AgentHostingMode { get; private set; }

        /// <summary>
        /// The type of entity used to identify the container.
        /// </summary>
        internal KubernetesEntityType SourceEntityType { get; private set; }

        /// <summary>
        /// The name of the namespace where the source container is running.
        /// </summary>
        public string NamespaceName { get; private set; }

        /// <summary>
        /// Name of the container. This is required in case of multiple containers running in the same pod.
        /// </summary>
        public string ContainerName { get; private set; }

        /// <summary>
        /// The container.
        /// </summary>
        public V1Container Container { get; private set; }

        /// <summary>
        /// Name of the pod hosting the container
        /// </summary>
        public string PodName { get; private set; }

        /// <summary>
        /// The pod instance hosting the container
        /// </summary>
        public V1Pod Pod { get; private set; }

        /// <summary>
        /// Name of the deployment hosting the container
        /// </summary>
        public string DeploymentName { get; private set; }

        /// <summary>
        /// Deployment hosting the container
        /// </summary>
        public V1Deployment Deployment { get; private set; }

        /// <summary>
        /// Name of the service exposing the container.
        /// </summary>
        /// <remarks>
        /// For routing scenario, the new pod contains a label
        /// <see cref="Common.Constants.Routing.RoutingLabelPrefix">/<see cref="Common.Constants.Routing.RouteFromLabelName">
        /// pointing to this service
        /// </remarks>
        public string ServiceName { get; private set; }

        /// <summary>
        /// Service exposing the container.
        /// </summary>
        public V1Service Service { get; private set; }

        /// <summary>
        /// The routing header provided by the user. <see cref="Common.Constants.Routing.KubernetesRouteAsHeaderName"/>
        /// </summary>
        /// <remarks>
        /// This header value is added to the new pod using <see cref="Common.Constants.Routing.RouteFromLabelName"/> label.
        /// </remarks>
        public string RoutingHeaderValue { get; private set; }

        public IEnumerable<string> RoutingManagerFeatureFlags { get; private set; }

        /// <summary>
        /// Filepath to a .yaml file containing configuration options/overrides for the local process.
        /// </summary>
        public string LocalProcessConfigFilePath { get; private set; }

        /// <summary>
        /// StatefulSet hosting the container
        /// </summary>
        public V1StatefulSet StatefulSet { get; private set; }

        /// <summary>
        /// Name of the statefulset hosting the container
        /// </summary>
        public string StatefulSetName { get; private set; }

        #region Static Constructors

        /// <summary>
        /// Create a new pod with remote agent.
        /// </summary>
        public static RemoteContainerConnectionDetails CreatingNewPod(string namespaceName, string localProcessConfigFilePath = Constants.Config.FilePath)
        {
            var connectionDetails = new RemoteContainerConnectionDetails
            {
                AgentHostingMode = RemoteAgentHostingMode.NewPod,
                NamespaceName = namespaceName ?? throw new ArgumentNullException($"{nameof(namespaceName)} must be specified to connect to a new container."),
                LocalProcessConfigFilePath = localProcessConfigFilePath
            };
            return connectionDetails;
        }

        /// <summary>
        /// Clone pod spec from existing pod and replace container with remote agent.
        /// </summary>
        public static RemoteContainerConnectionDetails CreatingNewPodWithContextFromExistingPod(
            string namespaceName,
            string podName,
            string containerName = null,
            string localProcessConfigFilePath = Constants.Config.FilePath)
        {
            var connectionDetails = new RemoteContainerConnectionDetails
            {
                AgentHostingMode = RemoteAgentHostingMode.NewPodWithContext,
                SourceEntityType = KubernetesEntityType.Pod,
                NamespaceName = namespaceName ?? throw new ArgumentNullException($"{nameof(namespaceName)} must be specified to copy the context from an existing container."),
                PodName = podName ?? throw new ArgumentNullException($"{nameof(podName)} must be specified to copy the context from an existing container."),
                ContainerName = containerName,
                LocalProcessConfigFilePath = localProcessConfigFilePath
            };
            return connectionDetails;
        }

        /// <summary>
        /// Replace a container in the pod with remote agent.
        /// </summary>
        public static RemoteContainerConnectionDetails ReplacingExistingContainerInPod(
            string namespaceName,
            string podName,
            string containerName = null,
            string localProcessConfigFilePath = Constants.Config.FilePath)
        {
            var connectionDetails = new RemoteContainerConnectionDetails
            {
                AgentHostingMode = RemoteAgentHostingMode.Replace,
                SourceEntityType = KubernetesEntityType.Pod,
                NamespaceName = namespaceName ?? throw new ArgumentNullException($"{nameof(namespaceName)} must be specified to replace an existing container."),
                PodName = podName ?? throw new ArgumentNullException($"{nameof(podName)} must be specified to replace an existing container."),
                ContainerName = containerName,
                LocalProcessConfigFilePath = localProcessConfigFilePath
            };
            return connectionDetails;
        }

        /// <summary>
        /// Replace a container in pod managed by a deployment with remote agent.
        /// </summary>
        public static RemoteContainerConnectionDetails ReplacingExistingContainerInDeployment(
            string namespaceName,
            string deploymentName,
            string containerName = null,
            string localProcessConfigFilePath = Constants.Config.FilePath)
        {
            var connectionDetails = new RemoteContainerConnectionDetails
            {
                AgentHostingMode = RemoteAgentHostingMode.Replace,
                SourceEntityType = KubernetesEntityType.Deployment,
                NamespaceName = namespaceName ?? throw new ArgumentNullException($"{nameof(namespaceName)} must be specified to replace an existing container."),
                DeploymentName = deploymentName ?? throw new ArgumentNullException($"{nameof(deploymentName)} must be specified to replace an existing container."),
                ContainerName = containerName,
                LocalProcessConfigFilePath = localProcessConfigFilePath
            };
            return connectionDetails;
        }

        /// <summary>
        /// Replace a container in pod exposed by a service with remote agent.
        /// </summary>
        public static RemoteContainerConnectionDetails ReplacingExistingContainerInService(string namespaceName, string serviceName, string containerName = null, string localProcessConfigFilePath = Constants.Config.FilePath)
        {
            var connectionDetails = new RemoteContainerConnectionDetails
            {
                AgentHostingMode = RemoteAgentHostingMode.Replace,
                SourceEntityType = KubernetesEntityType.Service,
                NamespaceName = namespaceName ?? throw new ArgumentNullException($"{nameof(namespaceName)} must be specified to replace an existing container."),
                ServiceName = serviceName ?? throw new ArgumentNullException($"{nameof(serviceName)} must be specified to replace an existing container."),
                ContainerName = containerName,
                LocalProcessConfigFilePath = localProcessConfigFilePath
            };
            return connectionDetails;
        }

        /// <summary>
        /// Create new pod by cloning the pod spec of the pod exposed by the service for routing scenario.
        /// </summary>
        public static RemoteContainerConnectionDetails CreatingNewPodWithContextFromExistingService(
            string namespaceName,
            string serviceName,
            string routingHeaderValue,
            string containerName = null,
            string localProcessConfigFilePath = Constants.Config.FilePath,
            IEnumerable<string> routingManagerFeatureFlags = null)
        {
            var connectionDetails = new RemoteContainerConnectionDetails
            {
                AgentHostingMode = RemoteAgentHostingMode.NewPodWithContext,
                SourceEntityType = KubernetesEntityType.Service,
                NamespaceName = namespaceName ?? throw new ArgumentNullException($"{nameof(namespaceName)} must be specified to copy the context from an existing container."),
                ServiceName = serviceName ?? throw new ArgumentNullException($"{nameof(serviceName)} must be specified to copy the context from an existing container."),
                RoutingHeaderValue = routingHeaderValue ?? throw new ArgumentNullException($"{nameof(routingHeaderValue)} must be specified to copy the context from an existing container."),
                ContainerName = containerName,
                LocalProcessConfigFilePath = localProcessConfigFilePath,
                RoutingManagerFeatureFlags = routingManagerFeatureFlags
            };
            return connectionDetails;
        }

        #endregion Static Constructors

        #region Update Identifiers

        /// <summary>
        /// Update container name
        /// </summary>
        public void UpdateContainerName(string containerName)
        {
            this.ContainerName = containerName;
        }

        /// <summary>
        /// Change the source entity to pod and update pod name
        /// </summary>
        public void UpdateSourceEntityTypeToPod(V1Pod pod)
        {
            this.SourceEntityType = KubernetesEntityType.Pod;
            this.Pod = pod;
            this.PodName = pod.Metadata?.Name;
        }

        /// <summary>
        /// Change the source entity to deployment and update deployment name
        /// </summary>
        public void UpdateSourceEntityTypeToDeployment(V1Deployment deployment)
        {
            this.SourceEntityType = KubernetesEntityType.Deployment;
            this.Deployment = deployment;
            this.DeploymentName = deployment.Metadata.Name;
        }

        /// <summary>
        /// Change the source entity to statefulSet and update statefulset name
        /// </summary>
        public void UpdateSourceEntityTypeToStatefulSet(V1StatefulSet statefulSet)
        {
            this.SourceEntityType = KubernetesEntityType.StatefulSet;
            this.StatefulSet = statefulSet;
            this.StatefulSetName = statefulSet.Metadata.Name;
        }

        /// <summary>
        /// The user might specify a container name, but we still want to verify that the container exists and cache it here
        /// </summary>
        /// <param name="container"></param>
        public void UpdateContainerDetails(V1Container container)
        {
            this.Container = container;
            this.ContainerName = container.Name;
        }

        /// <summary>
        /// The user might specify a service name, but we still want to verify that the service exists and cache it here
        /// </summary>
        /// <param name="service"></param>
        public void UpdateServiceDetails(V1Service service)
        {
            this.Service = service;
            this.ServiceName = service.Metadata.Name;
        }

        /// <summary>
        /// The user might specify a deployment name, but we still want to verify that the deployment exists and cache it here
        /// </summary>
        /// <param name="deployment"></param>
        public void UpdateDeploymentDetails(V1Deployment deployment)
        {
            this.Deployment = deployment;
            this.DeploymentName = deployment.Metadata.Name;
        }

        /// <summary>
        /// The user might specify a stateful set name, but we still want to verify that the stateful set exists and cache it here
        /// </summary>
        /// <param name="deployment"></param>
        public void UpdateStatefulSetDetails(V1StatefulSet statefulSet)
        {
            this.StatefulSet = statefulSet;
            this.StatefulSetName = statefulSet.Metadata.Name;
        }

        /// <summary>
        /// The user might specify a pod name, but we still want to verify that the pod exists and cache it here
        /// </summary>
        /// <param name="deployment"></param>
        public void UpdatePodDetails(V1Pod pod)
        {
            this.Pod = pod;
            this.PodName = pod.Metadata.Name;
        }

        #endregion Update Identifiers
    }
}