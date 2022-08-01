// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Attributes;
using Microsoft.BridgeToKubernetes.RoutingManager.Traefik;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    /// <summary>
    /// Kubernetes resource types.
    /// </summary>
    internal enum KubernetesResourceType
    {
        /// <summary>
        /// Namespace.
        /// </summary>
        [StringValue("Namespace")]
        Namespace,

        /// <summary>
        /// Pod.
        /// </summary>
        [StringValue("Pod")]
        Pod,

        /// <summary>
        /// Deployment.
        /// </summary>
        [StringValue("Deployment")]
        Deployment,

        /// <summary>
        /// Service
        /// </summary>
        [StringValue("Service")]
        Service,

        /// <summary>
        /// Secret.
        /// </summary>
        [StringValue("Secret")]
        Secret,

        /// <summary>
        /// Config map.
        /// </summary>
        [StringValue("ConfigMap")]
        ConfigMap,

        /// <summary>
        /// Daemon set.
        /// </summary>
        [StringValue("DaemonSet")]
        DaemonSet,

        /// <summary>
        /// Stateful Set
        /// </summary>
        [StringValue("StatefulSet")]
        StatefulSet,

        /// <summary>
        /// Ingress
        /// </summary>
        [StringValue("Ingress")]
        Ingress,

        /// <summary>
        /// IngressRoute - this is a CRD supported by Traefik
        /// </summary>
        /// <remarks>https://github.com/projectcontour/contour/blob/main/design/ingressroute-design.md</remarks>
        [StringValue("IngressRoute")]
        IngressRoute,

        /// <summary>
        /// Mutating Webhook
        /// </summary>
        [StringValue("MutatingWebhookConfiguration")]
        MutatingWebhookConfiguration,

        /// <summary>
        /// Cluster Role binding
        /// </summary>
        [StringValue("ClusterRoleBinding")]
        ClusterRoleBinding,

        /// <summary>
        /// Service Account
        /// </summary>
        [StringValue("ServiceAccount")]
        ServiceAccount,

        /// <summary>
        /// ClusterRole
        /// </summary>
        [StringValue("ClusterRole")]
        ClusterRole,

        /// <summary>
        /// Job
        /// </summary>
        [StringValue("Job")]
        Job
    }

    /// <summary>
    /// IKubernetesClient interface encapsulates Kubernetes functionalities Bridge to Kubernetes uses.
    /// </summary>
    /// <remarks>
    /// This interface is intended to represent vanilla Kubernetes functionality ONLY.
    /// E.g. Methods should only deal with types from the <see cref="k8s.Models"/> namespace, NOT any of our clones/extensions.
    /// Anything considered "Mindaro-specific" should live somewhere else.
    /// </remarks>
    internal interface IKubernetesClient
    {
        string HostName { get; }

        /// <summary>
        /// Retrieve the list of user namespaces.
        /// </summary>
        /// <param name="labels">Labels to filter the namespaces on.</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<V1NamespaceList> ListNamespacesAsync(IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken));

        #region Pods

        /// <summary>
        /// Get first pod that matches with input label
        /// </summary>
        Task<V1Pod> GetFirstNamespacedPodWithLabelWithRunningContainerAsync(string namespaceName, string containerName, KeyValuePair<string, string> label, CancellationToken cancellationToken);

        /// <summary>
        /// List all pods that belong to a deployment
        /// </summary>
        Task<V1PodList> ListPodsForDeploymentAsync(string namespaceName, string deploymentName, CancellationToken cancellationToken);

        /// <summary>
        /// Get V1 pod.
        /// </summary>
        Task<V1Pod> GetV1PodAsync(string namespaceName, string podName, CancellationToken cancellationToken);

        /// <summary>
        /// List pods in specified namespace.
        /// </summary>
        Task<V1PodList> ListPodsInNamespaceAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a pod from the pod spec.
        /// </summary>
        Task<V1Pod> CreateV1PodAsync(string namespaceName, V1Pod pod, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Patch a namespaced pod.
        /// </summary>
        Task<V1Pod> PatchV1PodAsync(string namespaceName, string podName, V1Patch podPatch, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Delete V1 pod.
        /// </summary>
        Task DeleteV1PodAsync(string namespaceName, string podName, CancellationToken cancellationToken);

        /// <summary>
        /// Watch pod in the namespace
        /// </summary>
        Task<HttpOperationResponse<V1PodList>> WatchV1PodAsync(string namespaceName, string podName, int timeoutSeconds, CancellationToken cancellationToken);

        /// <summary>
        /// Start port forwarding one or more ports for a pod
        /// </summary>
        Task<WebSocket> WebSocketPodPortForwardAsync(string namespaceName, string podName, IEnumerable<int> remotePorts, string webSocketProtocol = null, CancellationToken cancellationToken = default(CancellationToken));

        #endregion Pods

        #region Services

        /// <summary>
        /// Get a V1 service in a namespace
        /// </summary>
        Task<V1Service> GetV1ServiceAsync(string namespaceName, string serviceName, CancellationToken cancellationToken);

        /// <summary>
        /// List services in specified namespace.
        /// </summary>
        Task<V1ServiceList> ListServicesInNamespaceAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates or replace a service from the service spec.
        /// </summary>
        Task<V1Service> CreateOrReplaceV1ServiceAsync(string namespaceName, V1Service service, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a service from the service spec
        /// </summary>
        Task<V1Service> ReplaceV1ServiceAsync(string namespaceName, V1Service service, string name, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Deletes a service from the namespace
        /// </summary>
        Task<V1Service> DeleteV1ServiceAsync(string namespaceName, string name, CancellationToken cancellationToken = default(CancellationToken));
        
        /// <summary>
        /// Create a service in a namespace
        /// </summary>
        Task<V1Service> CreateNamespacedServiceAsync(string namespaceName, V1Service service, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Lists the load balancer services in a namespace
        /// </summary>
        public Task<IEnumerable<V1Service>> ListLoadBalancerServicesInNamespaceAsync(string namespaceName, CancellationToken cancellationToken);

        #endregion Services

        #region Endpoints

        /// <summary>
        /// Retrieves an endpoint object in the specified namespace
        /// </summary>
        Task<V1Endpoints> GetEndpointInNamespaceAsync(string endpointName, string namespaceName, CancellationToken cancellationToken);

        /// <summary>
        /// List endpoints in specified namespace.
        /// </summary>
        Task<V1EndpointsList> ListEndpointsInNamespaceAsync(string namespaceName, CancellationToken cancellationToken, IEnumerable<KeyValuePair<string, string>> labels = null);

        #endregion Endpoints

        #region Replica Sets

        /// <summary>
        /// Get V1 replica set
        /// </summary>
        Task<V1ReplicaSet> GetV1ReplicaSetAsync(string namespaceName, string replicaSetName, CancellationToken cancellationToken);

        /// <summary>
        /// List replica sets in specified namespace.
        /// </summary>
        Task<V1ReplicaSetList> ListReplicaSetsInNamespaceAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken));

        #endregion Replica Sets

        #region Stateful Sets

        /// <summary>
        /// Get V1 stateful set
        /// </summary>
        Task<V1StatefulSet> GetV1StatefulSetAsync(string namespaceName, string statefulSetName, CancellationToken cancellationToken);

        /// <summary>
        /// List all pods that belong to a stateful set
        /// </summary>
        Task<V1PodList> ListPodsForStatefulSetAsync(string namespaceName, string statefulSetName, CancellationToken cancellationToken);

        /// <summary>
        /// Patch a namespaced stateful set.
        /// </summary>
        Task<V1StatefulSet> PatchV1StatefulSetAsync(string namespaceName, string statefulSetName, V1Patch statefulSetPatch, CancellationToken cancellationToken = default(CancellationToken));

        #endregion Stateful Sets

        #region Deployments

        /// <summary>
        /// Create or replace a namespaced deployment.
        /// </summary>
        Task<V1Deployment> CreateOrReplaceV1DeploymentAsync(string namespaceName, V1Deployment deployment, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Creates a namespaced deployment.
        /// </summary>
        Task<V1Deployment> CreateNamespacedDeploymentAsync(string namespaceName, V1Deployment deployment, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Replaces a namespaced deployment.
        /// </summary>
        Task<V1Deployment> ReplaceNamespacedDeploymentAsync(string namespaceName, V1Deployment deployment, string name, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Read a namespaced deployment status.
        /// </summary>
        Task<V1Deployment> ReadNamespacedDeploymentStatusAsync(string namespaceName, string name, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// List deployments in specified namespace
        /// </summary>
        Task<V1DeploymentList> ListDeploymentsInNamespaceAsync(string namespaceName, CancellationToken cancellationToken);

        /// <summary>
        /// List deployments in specified namespace
        /// </summary>
        Task<V1Status> DeleteDeploymentsInNamespaceAsync(string namespaceName, string name, CancellationToken cancellationToken);

        /// <summary>
        /// Patch a namespaced deployment.
        /// </summary>
        Task<V1Deployment> PatchV1DeploymentAsync(string namespaceName, string deploymentName, V1Patch deploymentPatch, CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Watch deployment in the namespace
        /// </summary>
        Task<HttpOperationResponse<V1DeploymentList>> WatchV1DeploymentAsync(string namespaceName, string deploymentName, int timeoutSeconds, CancellationToken cancellationToken);

        /// <summary>
        /// Get V1 deployment
        /// </summary>
        Task<V1Deployment> GetV1DeploymentAsync(string namespaceName, string deploymentName, CancellationToken cancellationToken);

        #endregion Deployments

        #region Secrets

        /// <summary>
        /// Read a secret in specified namespace
        /// </summary>
        Task<V1Secret> ReadNamespacedSecretAsync(string namespaceName, string secretName, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a secret in the specified namespace
        /// </summary>
        Task<V1Secret> CreateNamespacedSecretAsync(string namespaceName, V1Secret secret, CancellationToken cancellationToken);

        #endregion Secrets

        #region Jobs

        /// <summary>
        /// Creates a job in the specified namespace
        /// </summary>
        Task<V1Job> CreateNamespacedJobAsync(string namespaceName, V1Job job, CancellationToken cancellationToken);

        #endregion Jobs

        #region Ingress

        /// <summary>
        /// List ingresses in a namespace
        /// </summary>
        Task<V1IngressList> ListIngressesInNamespaceAsync(string namespaceName, CancellationToken cancellationToken);
        
        /// <summary>
        /// Create an ingress in a namespace
        /// </summary>
        Task<V1Ingress> CreateNamespacedIngressAsync(string namespaceName, V1Ingress body, CancellationToken cancellationToken);

        /// <summary>
        /// Deletes an ingress from a namespace
        /// </summary>
        Task<V1Status> DeleteNamespacedIngressAsync(string namespaceName, string name, CancellationToken cancellationToken);

        /// <summary>
        /// Replaces an ingress in a namespace
        /// </summary>
        Task<V1Ingress> ReplaceNamespacedIngress1Async(string namespaceName, V1Ingress body, string name, CancellationToken cancellationToken);

        #endregion Ingress

        #region IngressRoute

        /// <summary>
        /// List IngressRoutes (CRD for Traefik) in a namespace
        /// </summary>
        /// <remarks><see cref="IngressRoute"/>IngressRoute</remarks>
        Task<IEnumerable<IngressRoute>> ListNamespacedIngressRoutesAsync(string namespaceName, CancellationToken cancellationToken);

        /// <summary>
        /// Create an IngressRoute in a namespace
        /// </summary>
        /// <remarks><see cref="IngressRoute"/>IngressRoute</remarks>
        Task<bool> ApplyNamespacedIngressRouteAsync(string namespaceName, IngressRoute ingressRoute, CancellationToken cancellationToken);

        /// <summary>
        /// Delete an IngressRoute in a namespace
        /// </summary>
        /// <remarks><see cref="IngressRoute"/>IngressRoute</remarks>
        Task<bool> DeleteNamespacedIngressRouteAsync(string namespaceName, string ingressRouteName, CancellationToken cancellationToken);

        #endregion IngressRoute

        #region ConfigMap

        /// <summary>
        /// Get config map in a namespace
        /// </summary>
        Task<V1ConfigMap> GetConfigMapAsync(string namespaceName, string configMapName, CancellationToken cancellationToken);

        /// <summary>
        /// Create config map in a namespace
        /// </summary>
        Task<V1ConfigMap> CreateNamespacedConfigMapAsync(string namespaceName, V1ConfigMap configMap, CancellationToken cancellationToken);

        /// <summary>
        /// Delete a config map from a namespace
        /// </summary>
        Task<V1Status> DeleteNamespacedConfigMapAsync(string namespaceName, string name, CancellationToken cancellationToken);

        /// <summary>
        /// Replaces a config map in a namespace
        /// </summary>
        Task<V1ConfigMap> ReplaceNamespacedConfigMapAsync(string namespaceName, V1ConfigMap configMap, string name, CancellationToken cancellationToken);

        /// <summary>
        /// Lists the config maps in a namespace
        /// </summary>
        Task<V1ConfigMapList> ListNamespacedConfigMapAsync(string namespaceName, CancellationToken cancellationToken);

        #endregion ConfigMap

        /// <summary>
        /// Create or replace V1 service account
        /// </summary>
        /// <returns></returns>
        Task CreateServiceAccountIfNotExists(string namespaceName, V1ServiceAccount v1ServiceAccount, CancellationToken cancellationToken);

        /// <summary>
        /// Create or replace namespaced role
        /// </summary>
        Task<V1Role> CreateOrReplaceV1RoleInNamespaceAsync(V1Role v1Role, string namespaceName, CancellationToken cancellationToken);

        /// <summary>
        /// Create or replace namespaced role binding
        /// </summary>
        Task<V1RoleBinding> CreateOrReplaceV1RoleBindingInNamespaceAsync(V1RoleBinding V1RoleBinding, string namespaceName, CancellationToken cancellationToken);

        /// <summary>
        /// Execute a kubectl command asynchronously with retries
        /// </summary>
        /// <returns>kubectl exit code</returns>
        Task<int> InvokeShortRunningKubectlCommandWithRetriesAsync(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            long maxWaitTimeInSeconds = 300,
            uint numberOfAttempts = 3,
            long delayIntervalInMilliseconds = 10000,
            long maxDelayIntervalInSeconds = 30,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Execute a short running kubectl command and log the result as a dependency call
        /// </summary>
        /// <returns>kubectl exit code</returns>
        int InvokeShortRunningKubectlCommand(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            bool shouldIgnoreErrors = false,
            int timeoutMs = 30000,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Execute a long-running kubectl command and log the result as a dependency call
        /// </summary>
        /// <returns>kubectl exit code</returns>
        int InvokeLongRunningKubectlCommand(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            bool shouldIgnoreErrors = false,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Execute a kubectl command asynchronously and log the result as a dependency call
        /// </summary>
        /// <returns>kubectl exit code</returns>
        Task<int> InvokeShortRunningKubectlCommandAsync(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            bool shouldIgnoreErrors = false,
            int timeoutMs = 30000,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Execute a kubectl command on every item in a collection in parallel and log the overall result as a dependency call
        /// </summary>
        Task InvokeShortRunningKubectlCommandForEachAsync<T>(
            T[] items,
            KubernetesCommandName commandName,
            Func<T, string> command,
            CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        /// Get all the environment variables seen from within the specified container
        /// </summary>
        /// <param name="namespaceName"></param>
        /// <param name="podName"></param>
        /// <param name="containerName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<IDictionary<string, string>> GetContainerEnvironmentAsync(
            string namespaceName,
            string podName,
            string containerName,
            CancellationToken cancellationToken);
    }
}