// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.KubeConfigModels;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.RoutingManager.Traefik;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    /// <summary>
    /// <see cref="IKubernetesClient"/>
    /// </summary>
    /// <remarks>
    /// This interface is intended to represent vanilla Kubernetes functionality ONLY.
    /// E.g. Methods should only deal with types from the <see cref="k8s.Models"/> namespace, NOT any Bridge to Kubernetes clones/extensions.
    /// Anything considered "Bridge to Kubernetes-specific" should probably live somewhere else.
    /// </remarks>
    internal class KubernetesClient : KubectlImpl, IKubernetesClient
    {
        private readonly Dictionary<string, object> _loggingProperties = new Dictionary<string, object> { };

        public delegate IKubernetesClient InClusterFactory(bool useInClusterConfig = true, string kubectlFilePath = null);

        private string _activeKubeConfigFilePath { get; }
        private K8SConfiguration _k8SConfiguration { get; }

        private Lazy<IKubernetes> _restClient;
        private IKubernetes RestClient => _restClient.Value;

        public delegate IKubernetesClient Factory(K8SConfiguration k8SConfiguration, string kubeConfigFilePath);

        public KubernetesClient(
            IK8sClientFactory kubeClientFactory,
            IFileSystem fileSystem,
            IPlatform platform,
            IEnvironmentVariables environmentVariables,
            ILog log,
            K8SConfiguration k8SConfiguration = null,
            string kubeConfigFilePath = null,
            string kubectlFilePath = null,
            bool useInClusterConfig = false)
            : base(fileSystem, platform, environmentVariables, log, kubectlFilePath)
        {
            if (!useInClusterConfig && string.IsNullOrEmpty(kubeConfigFilePath) && k8SConfiguration == null)
            {
                throw new InvalidOperationException($"{nameof(KubernetesClient)} must have either '{nameof(useInClusterConfig)} = true', '{nameof(k8SConfiguration)}' or '{nameof(kubeConfigFilePath)}' set!");
            }
            this._activeKubeConfigFilePath = kubeConfigFilePath;
            this._k8SConfiguration = k8SConfiguration;

            _restClient = new Lazy<IKubernetes>(() =>
            {
                if (useInClusterConfig)
                {
                    return kubeClientFactory.CreateFromInClusterConfig();
                }
                else if (_k8SConfiguration != null)
                {
                    return kubeClientFactory.CreateFromKubeConfig(_k8SConfiguration);
                }
                else
                {
                    return kubeClientFactory.CreateFromKubeConfigFile(kubeConfigFilePath);
                }
            });
        }

        public string HostName => RestClient.BaseUri.Host;

        #region List namespaces

        /// <summary>
        /// <see cref="IKubernetesClient.ListNamespacesAsync(IEnumerable{KeyValuePair{string, string}}, CancellationToken)"/>
        /// </summary>
        public async Task<V1NamespaceList> ListNamespacesAsync(IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                string labelSelector = GetLabelSelectorString(labels);
                return await RestClient.CoreV1.ListNamespaceAsync(labelSelector: labelSelector, cancellationToken: cancellationToken);
            }, nameof(ListNamespacesAsync), cancellationToken);

            return result?.Items == null ? new V1NamespaceList(new List<V1Namespace>()) : result;
        }

        #endregion List namespaces

        #region Deployments

        /// <summary>
        /// <see cref="IKubernetesClient.CreateOrReplaceV1DeploymentAsync(string, V1Deployment, CancellationToken)"/>
        /// </summary>
        public async Task<V1Deployment> CreateOrReplaceV1DeploymentAsync(string namespaceName, V1Deployment deployment, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                try
                {
                    return await RestClient.AppsV1.CreateNamespacedDeploymentAsync(deployment, namespaceName, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.Conflict)
                {
                    await RestClient.AppsV1.DeleteNamespacedDeploymentAsync(deployment.Metadata.Name, deployment.Metadata.NamespaceProperty);
                    return await RestClient.AppsV1.CreateNamespacedDeploymentAsync(deployment, namespaceName, cancellationToken: cancellationToken);
                }
            },
            nameof(CreateOrReplaceV1DeploymentAsync),
            cancellationToken: cancellationToken);
        }

        public async Task<V1Deployment> CreateNamespacedDeploymentAsync(string namespaceName, V1Deployment deployment, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.AppsV1.CreateNamespacedDeploymentAsync(deployment, namespaceName, cancellationToken: cancellationToken);
            }, nameof(CreateNamespacedDeploymentAsync), cancellationToken);
        }

        public async Task<V1Status> DeleteDeploymentsInNamespaceAsync(string namespaceName, string name, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.AppsV1.DeleteNamespacedDeploymentAsync(name, namespaceName, cancellationToken: cancellationToken);
            }, nameof(DeleteDeploymentsInNamespaceAsync), cancellationToken);
        }

        public async Task<V1Deployment> ReadNamespacedDeploymentStatusAsync(string namespaceName, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.AppsV1.ReadNamespacedDeploymentStatusAsync(name, namespaceName, cancellationToken: cancellationToken);
            }, nameof(ReadNamespacedDeploymentStatusAsync), cancellationToken);
        }

        public async Task<V1Deployment> ReplaceNamespacedDeploymentAsync(string namespaceName, V1Deployment deployment, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.AppsV1.ReplaceNamespacedDeploymentAsync(deployment, name, namespaceName, cancellationToken: cancellationToken);
            }, nameof(ReplaceNamespacedDeploymentAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListDeploymentsInNamespaceAsync(string, CancellationToken)"/>
        /// </summary>
        public async Task<V1DeploymentList> ListDeploymentsInNamespaceAsync(string namespaceName, CancellationToken cancellationToken)
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                return string.IsNullOrEmpty(namespaceName) ?
                    await RestClient.AppsV1.ListDeploymentForAllNamespacesAsync(cancellationToken: cancellationToken) :
                    await RestClient.AppsV1.ListNamespacedDeploymentAsync(namespaceName, cancellationToken: cancellationToken);
            }, nameof(ListDeploymentsInNamespaceAsync), cancellationToken);

            return result?.Items == null ? new V1DeploymentList(new List<V1Deployment>()) : result;
        }

        /// <summary>
        /// <see cref="IKubernetesClient.PatchV1DeploymentAsync(string, string, V1Patch, CancellationToken)"/>
        /// </summary>
        public async Task<V1Deployment> PatchV1DeploymentAsync(string namespaceName, string deploymentName, V1Patch deploymentPatch, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.AppsV1.PatchNamespacedDeploymentAsync(deploymentPatch, deploymentName, namespaceName, cancellationToken: cancellationToken), nameof(PatchV1DeploymentAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.WatchV1DeploymentAsync(string, string, int, CancellationToken)"/>
        /// </summary>
        public Task<HttpOperationResponse<V1DeploymentList>> WatchV1DeploymentAsync(string namespaceName, string deploymentName, int timeoutSeconds, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.AppsV1.ListNamespacedDeploymentWithHttpMessagesAsync(namespaceName, fieldSelector: $"metadata.name={deploymentName}", timeoutSeconds: timeoutSeconds, watch: true, cancellationToken: cancellationToken), nameof(WatchV1DeploymentAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListPodsForDeploymentAsync"/>
        /// </summary>
        public Task<V1PodList> ListPodsForDeploymentAsync(string namespaceName, string deploymentName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () =>
            {
                var deployment = await this.GetV1DeploymentAsync(namespaceName, deploymentName, cancellationToken);
                if (deployment == null)
                {
                    return null;
                }
                var pods = await this.ListPodsInNamespaceAsync(namespaceName, deployment.Spec.Selector.MatchLabels, cancellationToken);
                return pods;
            },
            nameof(ListPodsForDeploymentAsync),
            cancellationToken);
        }

        #endregion Deployments

        #region Secrets

        /// <summary>
        /// <see cref="IKubernetesClient.ReadNamespacedSecretAsync(string, string, CancellationToken)"/>
        /// </summary>
        public async Task<V1Secret> ReadNamespacedSecretAsync(string namespaceName, string secretName, CancellationToken cancellationToken)
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.CoreV1.ReadNamespacedSecretAsync(secretName, namespaceName, cancellationToken: cancellationToken);
            }, nameof(ReadNamespacedSecretAsync), cancellationToken);

            return result;
        }

        /// <summary>
        /// <see cref="IKubernetesClient.CreateNamespacedSecretAsync"/>
        /// </summary>
        public Task<V1Secret> CreateNamespacedSecretAsync(string namespaceName, V1Secret secret, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(() => RestClient.CoreV1.CreateNamespacedSecretAsync(secret, namespaceName, cancellationToken: cancellationToken), nameof(CreateNamespacedSecretAsync), cancellationToken);
        }

        #endregion Secrets

        #region Jobs

        /// <summary>
        /// <see cref="IKubernetesClient.CreateNamespacedJobAsync"/>
        /// </summary>
        public Task<V1Job> CreateNamespacedJobAsync(string namespaceName, V1Job job, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(() => RestClient.BatchV1.CreateNamespacedJobAsync(job, namespaceName, cancellationToken: cancellationToken), nameof(CreateNamespacedJobAsync), cancellationToken);
        }

        #endregion Jobs

        #region Pods

        /// <summary>
        /// <see cref="IKubernetesClient.GetV1PodAsync(string, string, CancellationToken)"/>
        /// </summary>
        public Task<V1Pod> GetV1PodAsync(string namespaceName, string podName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.ReadNamespacedPodAsync(podName, namespaceName, cancellationToken: cancellationToken), nameof(GetV1PodAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListPodsInNamespaceAsync(string, IEnumerable{KeyValuePair{string, string}}, CancellationToken)"/>
        /// </summary>
        public async Task<V1PodList> ListPodsInNamespaceAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                string labelSelector = GetLabelSelectorString(labels);
                return string.IsNullOrEmpty(namespaceName) ?
                    await RestClient.CoreV1.ListPodForAllNamespacesAsync(labelSelector: labelSelector, cancellationToken: cancellationToken)
                    : await RestClient.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: labelSelector, cancellationToken: cancellationToken);
            }, nameof(ListPodsInNamespaceAsync), cancellationToken);

            return result?.Items == null ? new V1PodList(new List<V1Pod>()) : result;
        }

        /// <summary>
        /// <see cref="IKubernetesClient.CreateV1PodAsync(string, V1Pod, CancellationToken)"/>
        /// </summary>
        public async Task<V1Pod> CreateV1PodAsync(string namespaceName, V1Pod pod, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.CreateNamespacedPodAsync(pod, namespaceName, cancellationToken: cancellationToken), nameof(CreateV1PodAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.PatchV1PodAsync(string, string, V1Patch, CancellationToken)"/>
        /// </summary>
        public async Task<V1Pod> PatchV1PodAsync(string namespaceName, string podName, V1Patch podPatch, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.PatchNamespacedPodAsync(podPatch, podName, namespaceName, cancellationToken: cancellationToken), nameof(PatchV1PodAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.DeleteV1PodAsync(string, string, CancellationToken)"/>
        /// </summary>
        public async Task DeleteV1PodAsync(string namespaceName, string podName, CancellationToken cancellationToken)
        {
            await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.DeleteNamespacedPodAsync(podName, namespaceName, cancellationToken: cancellationToken), nameof(DeleteV1PodAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.WatchV1PodAsync(string, string, int, CancellationToken)"/>
        /// </summary>
        public Task<HttpOperationResponse<V1PodList>> WatchV1PodAsync(string namespaceName, string podName, int timeoutSeconds, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.ListNamespacedPodWithHttpMessagesAsync(namespaceName, fieldSelector: $"metadata.name={podName}", timeoutSeconds: timeoutSeconds, watch: true, cancellationToken: cancellationToken), nameof(WatchV1PodAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.WebSocketPodPortForwardAsync(string, string, IEnumerable{int}, string, CancellationToken)"/>
        /// </summary>
        public Task<WebSocket> WebSocketPodPortForwardAsync(string namespaceName, string podName, IEnumerable<int> remotePorts, string webSocketProtocol = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.WebSocketNamespacedPodPortForwardAsync(podName, namespaceName, remotePorts, webSocketProtocol, cancellationToken: cancellationToken), nameof(WebSocketPodPortForwardAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.GetFirstNamespacedPodWithLabelWithRunningContainerAsync"/>
        /// </summary>
        public Task<V1Pod> GetFirstNamespacedPodWithLabelWithRunningContainerAsync(string namespaceName, string containerName, KeyValuePair<string, string> label, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () =>
            {
                V1Pod firstRunningPod = null;
                await WebUtilities.RetryUntilTimeWithWaitAsync(
                    async _ =>
                    {
                        var pods = await RestClient.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: $"{label.Key}={label.Value}", cancellationToken: cancellationToken);
                        firstRunningPod = pods.Items.FirstOrDefault(pod => pod.Status.Phase.EqualsIgnoreCase("Running"));
                        var containerStatus = firstRunningPod?.Status?.ContainerStatuses?.FirstOrDefault(c => c.Name.EqualsIgnoreCase(containerName));
                        if (pods == null || pods.Items == null || firstRunningPod == null || containerStatus?.State?.Running == null)
                        {
                            return false;
                        }
                        return true;
                    },
                    TimeSpan.FromMinutes(2),
                    TimeSpan.FromMilliseconds(500),
                    cancellationToken);
                return firstRunningPod;
            },
            nameof(GetFirstNamespacedPodWithLabelWithRunningContainerAsync),
            cancellationToken);
        }

        public Task<V1PodList> ListNamespacedPodAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ClientInvokeWrapperAsync(async () =>
            {
                string labelSelector = GetLabelSelectorString(labels);
                return await RestClient.CoreV1.ListNamespacedPodAsync(namespaceName, labelSelector: labelSelector, cancellationToken: cancellationToken);
            },
            nameof(ListNamespacedPodAsync),
            cancellationToken);
        }

        #endregion Pods

        #region Services

        /// <summary>
        /// <see cref="IKubernetesClient.GetV1ServiceAsync(string, string, CancellationToken)"/>
        /// </summary>
        public Task<V1Service> GetV1ServiceAsync(string namespaceName, string serviceName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.ReadNamespacedServiceAsync(serviceName, namespaceName, cancellationToken: cancellationToken), nameof(GetV1ServiceAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListServicesInNamespaceAsync(string, IEnumerable{KeyValuePair{string, string}}, CancellationToken)"/>
        /// </summary>
        public async Task<V1ServiceList> ListServicesInNamespaceAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                string labelSelector = GetLabelSelectorString(labels);
                return string.IsNullOrEmpty(namespaceName) ?
                    await RestClient.CoreV1.ListServiceForAllNamespacesAsync(labelSelector: labelSelector, cancellationToken: cancellationToken) :
                    await RestClient.CoreV1.ListNamespacedServiceAsync(namespaceName, labelSelector: labelSelector, cancellationToken: cancellationToken);
            }, nameof(ListServicesInNamespaceAsync), cancellationToken);

            return result?.Items == null ? new V1ServiceList(new List<V1Service>()) : result;
        }

        /// <summary>
        /// <see cref="IKubernetesClient.CreateOrReplaceV1ServiceAsync(string, V1Service, CancellationToken)"/>
        /// </summary>
        /// <returns></returns>
        public async Task<V1Service> CreateOrReplaceV1ServiceAsync(string namespaceName, V1Service service, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                try
                {
                    return await RestClient.CoreV1.CreateNamespacedServiceAsync(service, namespaceName, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.Conflict)
                {
                    try 
                    {
                        _log.Warning("Initial CreateNamespacedServiceAsync failed, deleting namespace");
                        await RestClient.CoreV1.DeleteNamespacedServiceAsync(service.Metadata.Name, namespaceName);
                    }
                    catch (JsonException ex)
                    {
                        // Delete service can through Json error when kubernetes server and client version are incompatible:
                        // 1.21 and 1.22 DeleteService returns v1.Status (6.0 client sdk)
                        // while in 1.23, DeleteService returns v1.Service (7.0+ client sdk)
                        // more details on this issue: https://github.com/kubernetes-client/csharp/issues/824
                        _log.Exception(ex);
                    }
                    return await RestClient.CoreV1.CreateNamespacedServiceAsync(service, namespaceName, cancellationToken: cancellationToken);

                }
            }, nameof(CreateOrReplaceV1ServiceAsync), cancellationToken);
        }

        public async Task<V1Service> ReplaceV1ServiceAsync(string namespaceName, V1Service service, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.CoreV1.ReplaceNamespacedServiceAsync(service, name, namespaceName, cancellationToken: cancellationToken);
            }, nameof(ReplaceV1ServiceAsync), cancellationToken);
        }

        public async Task<V1Service> CreateNamespacedServiceAsync(string namespaceName, V1Service service, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.CoreV1.CreateNamespacedServiceAsync(service, namespaceName, cancellationToken: cancellationToken);
            }, nameof(CreateNamespacedServiceAsync), cancellationToken);
        }

        public async Task<V1Service> DeleteV1ServiceAsync(string namespaceName, string name, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.CoreV1.DeleteNamespacedServiceAsync(name, namespaceName, cancellationToken: cancellationToken);
            }, nameof(DeleteV1ServiceAsync), cancellationToken);
        }

        public async Task<IEnumerable<V1Service>> ListLoadBalancerServicesInNamespaceAsync(string namespaceName, CancellationToken cancellationToken)
        {
            var services = await this.ListServicesInNamespaceAsync(namespaceName, cancellationToken: cancellationToken);
            return services?.Items == null ? new List<V1Service>() : services.Items.Where(svc => StringComparer.OrdinalIgnoreCase.Equals(svc.Spec.Type, KubernetesConstants.TypeStrings.LoadBalancer));
        }

        #endregion Services

        #region Endpoints

        /// <summary>
        /// <see cref="IKubernetesClient.GetEndpointInNamespaceAsync(string, string, CancellationToken)"/>
        /// </summary>
        public async Task<V1Endpoints> GetEndpointInNamespaceAsync(string endpointName, string namespaceName, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                return await RestClient.CoreV1.ReadNamespacedEndpointsAsync(endpointName, namespaceName, cancellationToken: cancellationToken);
            }, nameof(GetEndpointInNamespaceAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListEndpointsInNamespaceAsync(string, CancellationToken, IEnumerable{KeyValuePair{string, string}})"/>
        /// </summary>
        public async Task<V1EndpointsList> ListEndpointsInNamespaceAsync(string namespaceName, CancellationToken cancellationToken, IEnumerable<KeyValuePair<string, string>> labels = null)
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                string labelSelector = GetLabelSelectorString(labels);
                return await RestClient.CoreV1.ListNamespacedEndpointsAsync(namespaceName, labelSelector: labelSelector, cancellationToken: cancellationToken);
            }, nameof(ListEndpointsInNamespaceAsync), cancellationToken);

            return result?.Items == null ? new V1EndpointsList(new List<V1Endpoints>()) : result;
        }

        #endregion Endpoints

        #region Replica Sets

        /// <summary>
        /// <see cref="IKubernetesClient.GetV1ReplicaSetAsync(string, string, CancellationToken)"/>
        /// </summary>
        public Task<V1ReplicaSet> GetV1ReplicaSetAsync(string namespaceName, string replicaSetName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.AppsV1.ReadNamespacedReplicaSetAsync(replicaSetName, namespaceName, cancellationToken: cancellationToken), nameof(GetV1ReplicaSetAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListReplicaSetsInNamespaceAsync(string, IEnumerable{KeyValuePair{string, string}}, CancellationToken)"/>
        /// </summary>
        public async Task<V1ReplicaSetList> ListReplicaSetsInNamespaceAsync(string namespaceName, IEnumerable<KeyValuePair<string, string>> labels = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var result = await ClientInvokeWrapperAsync(async () =>
            {
                string labelSelector = GetLabelSelectorString(labels);
                return string.IsNullOrEmpty(namespaceName) ?
                    await RestClient.AppsV1.ListReplicaSetForAllNamespacesAsync(labelSelector: labelSelector, cancellationToken: cancellationToken) :
                    await RestClient.AppsV1.ListNamespacedReplicaSetAsync(namespaceName, labelSelector: labelSelector, cancellationToken: cancellationToken);
            }, nameof(ListReplicaSetsInNamespaceAsync), cancellationToken);

            return result?.Items == null ? new V1ReplicaSetList(new List<V1ReplicaSet>()) : result;
        }

        #endregion Replica Sets

        #region Stateful Sets

        /// <summary>
        /// <see cref="IKubernetesClient.GetV1StatefulSetAsync(string, string, CancellationToken)"/>
        /// </summary>
        public Task<V1StatefulSet> GetV1StatefulSetAsync(string namespaceName, string statefulSetName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.AppsV1.ReadNamespacedStatefulSetAsync(statefulSetName, namespaceName, cancellationToken: cancellationToken), nameof(GetV1StatefulSetAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ListPodsForStatefulSetAsync"/>
        /// </summary>
        public Task<V1PodList> ListPodsForStatefulSetAsync(string namespaceName, string statefulSetName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () =>
            {
                var statefulSet = await this.GetV1StatefulSetAsync(namespaceName, statefulSetName, cancellationToken);
                if (statefulSet == null)
                {
                    return null;
                }
                var pods = await this.ListPodsInNamespaceAsync(namespaceName, statefulSet.Spec.Selector.MatchLabels, cancellationToken);
                return pods;
            },
            nameof(ListPodsForStatefulSetAsync),
            cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.PatchV1StatefulSetAsync(string, string, V1Patch, CancellationToken)"/>
        /// </summary>
        public async Task<V1StatefulSet> PatchV1StatefulSetAsync(string namespaceName, string statefulSetName, V1Patch statefulSetPatch, CancellationToken cancellationToken = default(CancellationToken))
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.AppsV1.PatchNamespacedStatefulSetAsync(statefulSetPatch, statefulSetName, namespaceName, cancellationToken: cancellationToken), nameof(PatchV1StatefulSetAsync), cancellationToken);
        }

        #endregion Stateful Sets

        #region Ingresses

        /// <summary>
        /// <see cref="IKubernetesClient.ListIngressesInNamespaceAsync(string, CancellationToken)"/>
        /// </summary>
        public async Task<V1IngressList> ListIngressesInNamespaceAsync(string namespaceName, CancellationToken cancellationToken)
        {
            var result = await ClientInvokeWrapperAsync(async () => await RestClient.NetworkingV1.ListNamespacedIngressAsync(namespaceName, cancellationToken: cancellationToken), nameof(ListIngressesInNamespaceAsync), cancellationToken);
            return result?.Items == null ? new V1IngressList(new List<V1Ingress>()) : result;
        }

        public async Task<V1Ingress> CreateNamespacedIngressAsync(string namespaceName, V1Ingress body, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.NetworkingV1.CreateNamespacedIngressAsync(body, namespaceName, cancellationToken: cancellationToken), nameof(CreateNamespacedIngressAsync), cancellationToken);
        }

        public async Task<V1Ingress> ReplaceNamespacedIngress1Async(string namespaceName, V1Ingress body, string name, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.NetworkingV1.ReplaceNamespacedIngressAsync(body, name, namespaceName, cancellationToken: cancellationToken), nameof(ReplaceNamespacedIngress1Async), cancellationToken);
        }

        public async Task<V1Status> DeleteNamespacedIngressAsync(string namespaceName, string name, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.NetworkingV1.DeleteNamespacedIngressAsync(name, namespaceName, cancellationToken: cancellationToken), nameof(DeleteNamespacedIngressAsync), cancellationToken);
        }

        #endregion Ingresses

        #region IngressRoute

        /// <summary>
        /// <see cref="IKubernetesClient.ListNamespacedIngressRoutesAsync(string, CancellationToken)"/>
        /// </summary>
        public async Task<IEnumerable<IngressRoute>> ListNamespacedIngressRoutesAsync(string namespaceName, CancellationToken cancellationToken)
        {
            var outputSb = new StringBuilder();
            var errorSb = new StringBuilder();
            try
            {
                int errorCode = await InvokeShortRunningKubectlCommandAsync(
                    KubernetesCommandName.ListIngressRoutes,
                    $"-n {namespaceName} get ingressroutes -o json",
                    (output) => outputSb.Append(output),
                    (error) => errorSb.Append(error),
                    logOutput: false,
                    timeoutMs: 2000,
                    cancellationToken: cancellationToken);
                var errorString = errorSb.ToString();
                if (!string.IsNullOrWhiteSpace(errorString))
                {
                    _log.Warning("Error when retrieving ingressRoutes: {0}", errorString);
                    return new List<IngressRoute>();
                }

                if (errorCode != 0)
                {
                    return new List<IngressRoute>();
                }
            }
            catch (TimeoutException)
            {
                return new List<IngressRoute>();
            }

            var outputString = outputSb.ToString();
            var ingressRoutesObject = JsonHelpers.DeserializeObject<IngressRoutes>(outputString);
            return ingressRoutesObject.Items;
        }

        /// <summary>
        /// <see cref="IKubernetesClient.ApplyNamespacedIngressRouteAsync(string, IngressRoute, CancellationToken)"/>
        /// </summary>
        public Task<bool> ApplyNamespacedIngressRouteAsync(string namespaceName, IngressRoute ingressRoute, CancellationToken cancellationToken)
        {
            var tempFile = Path.GetTempFileName();
            var ingressRouteYaml = KubernetesYaml.Serialize(ingressRoute);
            File.WriteAllText(tempFile, ingressRouteYaml);

            // --overwrite : Automatically resolve conflicts between the modified and live configuration by using values from the modified configuration
            // --all       : Select all resources in the namespace
            var result = this.RunShortRunningCommand(
                        commandName: KubernetesCommandName.Apply,
                        command: $"apply -f {tempFile} -n {namespaceName} --overwrite --validate=true",
                        onStdOut: null,
                        onStdErr: null,
                        cancellationToken: cancellationToken);
            return Task.FromResult(result == 0);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.DeleteNamespacedIngressRouteAsync(string, string, CancellationToken)"/>
        /// </summary>
        public async Task<bool> DeleteNamespacedIngressRouteAsync(string namespaceName, string ingressRouteName, CancellationToken cancellationToken)
        {
            var errorSb = new StringBuilder();
            int errorCode = await InvokeShortRunningKubectlCommandWithRetriesAsync(
                KubernetesCommandName.DeleteIngressRoute,
                $"-n {namespaceName} delete ingressroute {ingressRouteName}",
                (error) => errorSb.Append(error),
                logOutput: false,
                cancellationToken: cancellationToken);
            var errorString = errorSb.ToString();
            if (!string.IsNullOrWhiteSpace(errorString))
            {
                _log.Warning("Error when retrieving ingressRoutes: {0}", errorString);
                return false;
            }

            return true;
        }

        #endregion IngressRoute

        #region Get resource

        /// <summary>
        /// <see cref="IKubernetesClient.GetConfigMapAsync(string, string, CancellationToken)"/>
        /// </summary>
        public async Task<V1ConfigMap> GetConfigMapAsync(string namespaceName, string configMapName, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.ReadNamespacedConfigMapAsync(configMapName, namespaceName, cancellationToken: cancellationToken), nameof(GetConfigMapAsync), cancellationToken);
        }

        public async Task<V1ConfigMap> CreateNamespacedConfigMapAsync(string namespaceName, V1ConfigMap configMap, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.CreateNamespacedConfigMapAsync(configMap, namespaceName, cancellationToken: cancellationToken), nameof(CreateNamespacedConfigMapAsync), cancellationToken);
        }

        public async Task<V1Status> DeleteNamespacedConfigMapAsync(string namespaceName, string name, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.DeleteNamespacedConfigMapAsync(name, namespaceName, cancellationToken: cancellationToken), nameof(DeleteNamespacedConfigMapAsync), cancellationToken);
        }

        public async Task<V1ConfigMap> ReplaceNamespacedConfigMapAsync(string namespaceName, V1ConfigMap configMap, string name, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.ReplaceNamespacedConfigMapAsync(configMap, name, namespaceName, cancellationToken: cancellationToken), nameof(ReplaceNamespacedConfigMapAsync), cancellationToken);
        }

        public async Task<V1ConfigMapList> ListNamespacedConfigMapAsync(string namespaceName, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () => await RestClient.CoreV1.ListNamespacedConfigMapAsync(namespaceName, cancellationToken: cancellationToken), nameof(ListNamespacedConfigMapAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.GetV1DeploymentAsync(string, string, CancellationToken)"/>
        /// </summary>
        public Task<V1Deployment> GetV1DeploymentAsync(string namespaceName, string deploymentName, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () => await RestClient.AppsV1.ReadNamespacedDeploymentAsync(deploymentName, namespaceName, cancellationToken: cancellationToken), nameof(GetV1DeploymentAsync), cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.CreateServiceAccountIfNotExists(string, V1ServiceAccount, CancellationToken)"/>
        /// </summary>
        public Task CreateServiceAccountIfNotExists(string namespaceName, V1ServiceAccount v1ServiceAccount, CancellationToken cancellationToken)
        {
            return ClientInvokeWrapperAsync(async () =>
            {
                try
                {
                    return await RestClient.CoreV1.CreateNamespacedServiceAccountAsync(v1ServiceAccount, namespaceName, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException e) when (((HttpOperationException)e).Response.StatusCode == HttpStatusCode.Conflict)
                {
                    // resource already exists, ignore the error
                    _log.Verbose("Service account already exists, leaving it untouched.");
                    return v1ServiceAccount;
                }
                catch (Exception e)
                {
                    _log.Exception(e);
                    throw;
                }
            },
            nameof(CreateServiceAccountIfNotExists),
            cancellationToken: cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.CreateOrReplaceV1RoleInNamespaceAsync(V1Role, string, CancellationToken)"/>
        /// </summary>
        public async Task<V1Role> CreateOrReplaceV1RoleInNamespaceAsync(V1Role v1Role, string namespaceName, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                try
                {
                    return await RestClient.RbacAuthorizationV1.CreateNamespacedRoleAsync(v1Role, namespaceName, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException e) when (((HttpOperationException)e).Response.StatusCode == HttpStatusCode.Conflict)
                {
                    // resource already exists, replace it
                    return await RestClient.RbacAuthorizationV1.ReplaceNamespacedRoleAsync(v1Role, v1Role.Metadata.Name, namespaceName, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    _log.Exception(e);
                    throw;
                }
            },
            nameof(CreateOrReplaceV1RoleInNamespaceAsync),
            cancellationToken: cancellationToken);
        }

        /// <summary>
        /// <see cref="IKubernetesClient.CreateOrReplaceV1RoleBindingInNamespaceAsync(V1RoleBinding, string, CancellationToken)"/>
        /// </summary>
        public async Task<V1RoleBinding> CreateOrReplaceV1RoleBindingInNamespaceAsync(V1RoleBinding v1RoleBinding, string namespaceName, CancellationToken cancellationToken)
        {
            return await ClientInvokeWrapperAsync(async () =>
            {
                try
                {
                    return await RestClient.RbacAuthorizationV1.CreateNamespacedRoleBindingAsync(v1RoleBinding, namespaceName, cancellationToken: cancellationToken);
                }
                catch (HttpOperationException e) when (((HttpOperationException)e).Response.StatusCode == HttpStatusCode.Conflict)
                {
                    // resource already exists, replace it
                    return await RestClient.RbacAuthorizationV1.ReplaceNamespacedRoleBindingAsync(v1RoleBinding, v1RoleBinding.Metadata.Name, namespaceName, cancellationToken: cancellationToken);
                }
                catch (Exception e)
                {
                    _log.Exception(e);
                    throw;
                }
            },
            nameof(CreateOrReplaceV1RoleBindingInNamespaceAsync),
            cancellationToken: cancellationToken);
        }

        #endregion Get resource

        #region Invoke Kubectl

        /// <summary>
        /// <see cref="IKubernetesClient.InvokeShortRunningKubectlCommandWithRetriesAsync(KubernetesCommandName, string, Action{string}, Action{string}, bool, long, uint, long, long, CancellationToken)"/>
        /// </summary>
        public async Task<int> InvokeShortRunningKubectlCommandWithRetriesAsync(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            long maxWaitTimeInSeconds = 300,
            uint numberOfAttempts = 3,
            long delayIntervalInMilliseconds = 10000,
            long maxDelayIntervalInSeconds = 30,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            int result = 1;
            await WebUtilities.RetryWithExponentialBackoffAsync(async attempt =>
            {
                try
                {
                    result = await InvokeShortRunningKubectlCommandAsync(commandName, command, onStdOut, onStdErr, logOutput, shouldIgnoreErrors: (attempt != numberOfAttempts), cancellationToken: cancellationToken);
                    return result == 0;
                }
                catch (TimeoutException te)
                {
                    // Surface the exception if this is the last try.
                    if (attempt == numberOfAttempts)
                    {
                        throw;
                    }

                    this._log.ExceptionAsWarning(te);
                    this._log.Warning("Timed out waiting for kubectl command to finish. Retrying.");
                    return false;
                }
            }, cancellationToken, maxWaitTimeInSeconds, numberOfAttempts, delayIntervalInMilliseconds, maxDelayIntervalInSeconds);

            return result;
        }

        /// <summary>
        /// <see cref="IKubernetesClient.InvokeShortRunningKubectlCommandAsync(KubernetesCommandName, string, Action{string}, Action{string}, bool, bool, int, CancellationToken)"/>
        /// </summary>
        public Task<int> InvokeShortRunningKubectlCommandAsync(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            bool shouldIgnoreErrors = false,
            int timeoutMs = 30000,
            CancellationToken cancellationToken = default(CancellationToken))
            => Task.Run(() =>
                InvokeShortRunningKubectlCommand(
                    commandName: commandName,
                    command: command,
                    onStdOut: onStdOut,
                    onStdErr: onStdErr,
                    logOutput: logOutput,
                    shouldIgnoreErrors: shouldIgnoreErrors,
                    timeoutMs: timeoutMs,
                    cancellationToken: cancellationToken));

        /// <summary>
        /// <see cref="IKubernetesClient.InvokeShortRunningKubectlCommandForEachAsync{T}(T[], KubernetesCommandName, Func{T, string}, CancellationToken)"/>
        /// </summary>
        public async Task InvokeShortRunningKubectlCommandForEachAsync<T>(
            T[] items,
            KubernetesCommandName commandName,
            Func<T, string> command,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            var results = await Task.WhenAll(items.Select(item =>
                Task.Run(() =>
                    InvokeShortRunningKubectlCommand(
                        commandName: commandName,
                        command: command(item),
                        cancellationToken: cancellationToken))));
        }

        public int InvokeShortRunningKubectlCommand(
           KubernetesCommandName commandName,
           string command,
           Action<string> onStdOut = null,
           Action<string> onStdErr = null,
           bool logOutput = false,
           bool shouldIgnoreErrors = false,
           int timeoutMs = 30000,
           CancellationToken cancellationToken = default)
        {
            return InvokeKubectlCommand(commandName, command, shortRunningOperation: true, timeoutMs: timeoutMs, onStdOut, onStdErr, logOutput, shouldIgnoreErrors, cancellationToken);
        }

        public int InvokeLongRunningKubectlCommand(
           KubernetesCommandName commandName,
           string command,
           Action<string> onStdOut = null,
           Action<string> onStdErr = null,
           bool logOutput = false,
           bool shouldIgnoreErrors = false,
           CancellationToken cancellationToken = default)
        {
            return InvokeKubectlCommand(commandName, command, shortRunningOperation: false, timeoutMs: null, onStdOut, onStdErr, logOutput, shouldIgnoreErrors, cancellationToken: cancellationToken);
        }

        private int InvokeKubectlCommand(
            KubernetesCommandName commandName,
            string command,
            bool shortRunningOperation,
            int? timeoutMs,
            Action<string> onStdOut = null,
            Action<string> onStdErr = null,
            bool logOutput = false,
            bool shouldIgnoreErrors = false,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (shortRunningOperation && timeoutMs == null)
            {
                throw new InvalidUsageException(this._log.OperationContext, "Short running kubectl command is being invoked without passing 'timeoutMs' parameter.");
            }

            string tempKubeconfigFile = _fileSystem.Path.GetTempFilePath();
            try
            {
                string arguments = command;
                if (_k8SConfiguration != null)
                {
                    // We do not have a kubeconfig file, that is needed for kubectl, so we create a temp one and point kubectl to it
                    var contents = KubernetesYaml.Serialize(_k8SConfiguration);
                    if (!string.IsNullOrEmpty(contents))
                    {
                        _fileSystem.WriteAllTextToFile(tempKubeconfigFile, contents);

                        arguments = $"--kubeconfig=\"{tempKubeconfigFile}\" {arguments}";
                    }
                }
                else if (!string.IsNullOrEmpty(_activeKubeConfigFilePath))
                {
                    // We do have a kubeconfig file, we should use that one.
                    // NOTE: this is especially relevant for when the kubectl command modifies the kubeconfig (e.g. when logging in with AAD to run a kubectl command)
                    arguments = $"--kubeconfig=\"{_activeKubeConfigFilePath}\" {arguments}";
                }

                var commandLogProperties = new Dictionary<string, object>();

                var outputBuilder = new StringBuilder();
                Action<string> stdOutHandler = output =>
                {
                    if (logOutput)
                    {
                        outputBuilder.AppendLine(output);
                    }
                    onStdOut?.Invoke(output);
                };

                Action<string> stdErrHandler = error =>
                {
                    commandLogProperties[Property.Error] = error;
                    onStdErr?.Invoke(error);
                };

                int exitCode;
                if (shortRunningOperation)
                {
                    exitCode = RunShortRunningCommand(
                        commandName,
                        arguments,
                        stdOutHandler,
                        stdErrHandler,
                        cancellationToken,
                        log137ExitCodeErrorAsWarning: shouldIgnoreErrors,
                        timeoutMs: timeoutMs.Value);
                }
                else
                {
                    exitCode = RunLongRunningCommand(
                        commandName,
                        arguments,
                        stdOutHandler,
                        stdErrHandler,
                        cancellationToken,
                        log137ExitCodeErrorAsWarning: shouldIgnoreErrors);
                }

                commandLogProperties[Property.ExitCode] = exitCode;

                var success = shortRunningOperation ? exitCode == 0
                                : (cancellationToken.IsCancellationRequested || exitCode == 0);

                if (logOutput)
                {
                    commandLogProperties[Property.Output] = new PII(outputBuilder.ToString());
                }

                // If shouldIgnoreErrors is set to true, this failure is expected in that case. Marking it as success.
                this._log.Dependency(Dependency.Kubernetes, Enum.GetName(typeof(KubernetesCommandName), commandName), success: success || shouldIgnoreErrors, properties: commandLogProperties);
                return exitCode;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempKubeconfigFile) && _fileSystem.FileExists(tempKubeconfigFile))
                {
                    _fileSystem.DeleteFile(tempKubeconfigFile);
                }
            }
        }

        #endregion Invoke Kubectl

        #region Containers

        public async Task<IDictionary<string, string>> GetContainerEnvironmentAsync(string namespaceName, string podName, string containerName, CancellationToken cancellationToken)
        {
            StringBuilder errorSb = new StringBuilder();
            var result = new Dictionary<string, string>();
            var latestError = string.Empty;

            Action<string> outputHandler = (string output) =>
            {
                var equalIndex = output.IndexOf("=");
                if (equalIndex == -1)
                {
                    _log.Verbose("Failed to find the '=' character in the output, ignoring the environment variable.");
                    return;
                }
                result.Add(output.Substring(0, equalIndex), output.Substring(equalIndex + 1));
            };

            var gotEnvSuccessfully = await WebUtilities.RetryUntilTimeWithWaitAsync(async (i) =>
            {
                var exitCode = this.RunShortRunningCommand(
                    KubernetesCommandName.GetContainerEnvironment,
                    $"exec {podName} -c {containerName} -n {namespaceName} -- env",
                    onStdOut: outputHandler,
                    onStdErr: (string error) => errorSb.Append(error),
                    cancellationToken:cancellationToken);
                if (exitCode == 0)
                {
                    return true;
                }

                latestError = errorSb.ToString();
                _log.Warning($"Failed to get the container environment: {latestError}");

                if (latestError.Contains("container not found", StringComparison.OrdinalIgnoreCase))
                {
                    // We want to retry if the container isn't in a running state yet.
                    errorSb.Clear();
                    return false;
                }

                throw new UserVisibleException(this._operationContext, CommonResources.FailedToGetTheContainerEnvironmentFormat, new PII(containerName), latestError);
            },
            maxWaitTime: TimeSpan.FromSeconds(15),
            waitInterval: TimeSpan.FromSeconds(2),
            cancellationToken: cancellationToken);

            return gotEnvSuccessfully ? result : throw new UserVisibleException(this._operationContext, CommonResources.FailedToGetTheContainerEnvironmentFormat, new PII(containerName), latestError);
        }

        #endregion Containers

        #region Private methods

        private string GetLabelSelectorString(IEnumerable<KeyValuePair<string, string>> labels)
        {
            string labelSelector = null;
            if (labels != null)
            {
                var valuePairs = labels.Select(pair => $"{pair.Key}={pair.Value}");
                labelSelector = String.Join(",", valuePairs);
            }
            return labelSelector;
        }

        /// <exception cref="TimeoutException"/>
        private async Task<T> ClientInvokeWrapperAsync<T>(Func<Task<T>> handler, string operation, CancellationToken cancellationToken = default(CancellationToken))
        {
            T result = default(T);
            var maxWait = TimeSpan.FromSeconds(30);
            var interval = TimeSpan.FromMilliseconds(100);
            string method = $"{nameof(KubernetesClient)}.{nameof(ClientInvokeWrapperAsync)}";
            var operationProperties = new Dictionary<string, object>(_loggingProperties);
            bool success = await WebUtilities.RetryUntilTimeAsync(async t =>
            {
                try
                {
                    result = await handler();

                    if (result == null)
                    {
                        throw new UnauthorizedAccessException("Kubernetes returned an Unauthorized (401) status code");
                    }

                    return true;
                }
                catch (HttpOperationException opEx) when (opEx.Response != null)
                {
                    if (opEx.Response.StatusCode == HttpStatusCode.NotFound)
                    {
                        result = default(T);
                        return true;
                    }

                    _log.Error($"{operation} threw {nameof(HttpOperationException)}: StatusCode='{opEx.Response.StatusCode}', ReasonPhrase='{opEx.Response.ReasonPhrase}', Content='{new PII(opEx.Response.Content)}'");
                    if (!string.IsNullOrEmpty(opEx.Response.Content))
                    {
                        // Try to construct an error message that takes advantage of all the info available to us
                        var deserializedContent = JsonHelpers.DeserializeObject<Dictionary<string, object>>(opEx.Response.Content);
                        if (deserializedContent != null && deserializedContent.TryGetValue("message", out object errorMessage))
                        {
                            var errorString = $"{opEx.Response.ReasonPhrase}: {new PII(errorMessage.ToString())}";
                            if (opEx.Response.StatusCode == HttpStatusCode.Forbidden)
                            {
                                // Include aka.ms link if this is an RBAC error
                                errorString = string.Format(CommonResources.IncludeRBACInformationFormat, Constants.Product.Name, "https://aka.ms/bridge-to-k8s-rbac", errorString);
                            }
                            throw new UserVisibleException(_log.OperationContext, errorString);
                        }
                    }

                    throw;
                }
                catch (HttpRequestException httpEx) when (httpEx.Message.Contains("No such host is known"))
                {
                    this._log.Warning(httpEx.Message);
                    throw new InvalidUsageException(this._log.OperationContext, CommonResources.NoKubernetesClusterFound, httpEx);
                }
                catch (HttpRequestException httpEx) when (httpEx.GetInnermostException() is IOException)
                {
                    // Retryable error
                    var error = $"{method} encountered retryable {nameof(HttpRequestException)}: {httpEx.Message}: {httpEx.GetInnermostException().Message}: {t.ToString()}";
                    operationProperties[Property.Error] = error;
                    this._log.Warning(error);
                }
                catch (HttpRequestException httpEx) when (httpEx.GetInnermostException() is SocketException)
                {
                    // Retryable error
                    var error = $"{method} encountered retryable {nameof(HttpRequestException)}: {httpEx.Message}: {httpEx.GetInnermostException().Message}: {t.ToString()}";
                    operationProperties[Property.Error] = error;
                    this._log.Warning(error);
                }
                catch (OperationCanceledException operationCanceledEx) when (operationCanceledEx.GetInnermostException() is IOException)
                {
                    // Retryable error
                    var error = $"{method} encountered retryable {nameof(OperationCanceledException)}: Error: {operationCanceledEx.ToString()}: {t.ToString()}";
                    operationProperties[Property.Error] = error;
                    this._log.Warning(error);
                }
                catch (IOException ioex)
                {
                    // Retryable error
                    var error = $"{method} encountered retryable {nameof(IOException)}: {ioex.Message}: {t.ToString()}";
                    operationProperties[Property.Error] = error;
                    this._log.Warning(error);
                }

                await Task.Delay(interval, cancellationToken);
                return false;
            }, maxWait, cancellationToken);

            if (!success)
            {
                this._log.Dependency(Dependency.Kubernetes, operation, success: false, properties: operationProperties);
                throw new TimeoutException($"{method} timed out after {maxWait.TotalSeconds} seconds");
            }
            this._log.Dependency(Dependency.Kubernetes, operation, success: true, properties: operationProperties);
            return result;
        }

        #endregion Private methods
    }
}
