// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    /// <summary>
    /// Management client for the routing manager.
    /// </summary>
    internal class RoutingManagementClient : ManagementClientBase, IRoutingManagementClient
    {
        private readonly string _namespaceName;
        private readonly IKubernetesClient _kubernetesClient;
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly ManagementClientExceptionStrategy _managementClientExceptionStrategy;
        private readonly Lazy<IImageProvider> _imageProvider;

        private readonly string RoutingManagerServiceAccountName = KubernetesUtilities.GetKubernetesResourceName(Routing.RoutingManagerNameLower, "-sa");
        private readonly string RoutingManagerRoleName = KubernetesUtilities.GetKubernetesResourceName(Routing.RoutingManagerNameLower, "-role");
        private readonly string RoutingManagerRoleBindingName = KubernetesUtilities.GetKubernetesResourceName(Routing.RoutingManagerNameLower, "-rolebinding");
        private readonly string RoutingManagerDeploymentName = KubernetesUtilities.GetKubernetesResourceName(Routing.RoutingManagerNameLower, "-deployment");

        public delegate RoutingManagementClient Factory(string namespaceName, string userAgent, string correlationId);

        public RoutingManagementClient(
            string namespaceName,
            string userAgent,
            string correlationId,
            IKubernetesClient kubernetesClient,
            ILog log,
            IOperationContext operationContext,
            IEnvironmentVariables environmentVariables,
            ManagementClientExceptionStrategy managementClientExceptionStrategy,
            Lazy<IImageProvider> imageProvider) : base(log, operationContext)
        {
            this._namespaceName = namespaceName;
            this._kubernetesClient = kubernetesClient;
            this._environmentVariables = environmentVariables;
            this._managementClientExceptionStrategy = managementClientExceptionStrategy;
            this._imageProvider = imageProvider;

            _operationContext.LoggingProperties[LoggingConstants.Property.IsRoutingEnabled] = true;
            _operationContext.UserAgent = userAgent;
            _operationContext.CorrelationId = correlationId + LoggingConstants.CorrelationIdSeparator + LoggingUtils.NewId();
        }

        /// <summary>
        /// <see cref="IRoutingManagementClient.DeployRoutingManagerAsync(CancellationToken)"/>
        /// </summary>
        public async Task<OperationResponse> DeployRoutingManagerAsync(CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                    Events.RoutingManagementClient.AreaName,
                    Events.RoutingManagementClient.Operations.DeployRoutingManager))
                    {
                        if (await ShouldDeployRoutingManagerAsync(cancellationToken))
                        {
                            // Create routing manager resources in the namespace.
                            _log.Info($"Creating routing manager resources in namespace '{0}'..", new PII(_namespaceName));
                            try
                            {
                                var createRoleTask = _kubernetesClient.CreateOrReplaceV1RoleInNamespaceAsync(this.PrepareV1Role(), _namespaceName, cancellationToken);
                                var createRoleBindingTask = _kubernetesClient.CreateOrReplaceV1RoleBindingInNamespaceAsync(this.PrepareV1RoleBinding(), _namespaceName, cancellationToken);
                                await Task.WhenAll(createRoleTask, createRoleBindingTask);
                            }
                            catch (HttpOperationException e) when (e.Response.StatusCode == (HttpStatusCode)422) // Unprocessable entity
                            {
                                _log.Info("This is a non-rbac cluster, ignoring errors from creating role and role binding.");
                            }
                            var createServiceAccount = _kubernetesClient.CreateServiceAccountIfNotExists(_namespaceName, this.PrepareV1ServiceAccount(), cancellationToken);
                            var createDeploymentTask = _kubernetesClient.CreateOrReplaceV1DeploymentAsync(_namespaceName, this.PrepareV1Deployment(), cancellationToken);
                            var createServiceTask = _kubernetesClient.CreateOrReplaceV1ServiceAsync(_namespaceName, this.PrepareV1Service(), cancellationToken);
                            await Task.WhenAll(createServiceAccount, createDeploymentTask, createServiceTask);
                            perfLogger.SetSucceeded();
                            return new OperationResponse(_operationContext);
                        }
                        else
                        {
                            _log.Info("Another debug session is in progress and Routing manager is already present. Skipping updating the Routing manager in namespace '{0}' to avoid any disruptions to existing users.", new PII(_namespaceName));
                            perfLogger.SetSucceeded();
                            return new OperationResponse(_operationContext);
                        }
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig($"Failed to deploy routing manager in namespace '{_namespaceName}'."));
        }

        /// <summary>
        /// <see cref="IRoutingManagementClient.GetStatusAsync(string, string, CancellationToken)"/>
        /// </summary>
        public async Task<OperationResponse<RoutingStatus>> GetStatusAsync(string podName, CancellationToken cancellationToken)
        {
            bool isPortForwardSuccessful = false;

            Action<string, int, ManualResetEvent, CancellationTokenSource> startPortForwardToRoutingManager = (podName, localPort, manualResetEvent, portForwardCancellationTokenSource) =>
                _kubernetesClient.InvokeLongRunningKubectlCommand(
                    KubernetesCommandName.PortForward,
                    $"port-forward pod/{podName} --pod-running-timeout=1s {localPort}:{Routing.RoutingManagerPort} --namespace {_namespaceName}",
                    onStdOut: (output) =>
                    {
                        if (!string.IsNullOrWhiteSpace(output))
                        {
                            _log.Info("Port forward to routing manager output : '{0}'", output);
                            if (output.Contains("Forwarding from"))
                            {
                                isPortForwardSuccessful = true;
                                manualResetEvent.Set();
                            }
                        }
                    },
                    onStdErr: (err) =>
                    {
                        if (!string.IsNullOrWhiteSpace(err))
                        {
                            isPortForwardSuccessful = false;
                            _log.Warning("Error during port forward to routing manager : {0}", err);
                            manualResetEvent.Set();
                            // Usually all runs show that the first time we port forward to the RM, it usually results in an error.
                            // Cancelling the portforward cancellation token in case of error, will avoid multiple http calls using
                            // the broken port-forward and set up a fresh port forward which should succeed.
                            portForwardCancellationTokenSource.Cancel();
                        }
                    },
                    cancellationToken: portForwardCancellationTokenSource.Token);

            Func<HttpResponseMessage, Task<RoutingStatus>> parseRoutingStatusFuncAsync = async (responseMessage) =>
            {
                RoutingStatus deserializedRoutingStatus = null;
                if (responseMessage.StatusCode != HttpStatusCode.OK)
                {
                    return new RoutingStatus(false, $"Response status code returned was {responseMessage.StatusCode}");
                }

                try
                {
                    var responseBody = await responseMessage.Content.ReadAsStringAsync();
                    deserializedRoutingStatus = JsonHelpers.DeserializeObject<RoutingStatus>(responseBody);
                }
                catch (JsonException ex)
                {
                    _log.Exception(ex);
                    throw new InvalidOperationException($"Failed to deserialize routing status: '{ex.Message}'.");
                }

                return deserializedRoutingStatus;
            };

            Func<int, CancellationToken, Task<RoutingStatus>> getRoutingStatusWithRetriesAsync = async (localPort, cancellationToken) =>
            {
                RoutingStatus routingStatus = null;
                using (var httpClient = new HttpClient())
                {
                    try
                    {
                        await WebUtilities.RetryUntilTimeWithWaitAsync(async _ =>
                        {
                            try
                            {
                                var routingStatusResponse = await httpClient.GetAsync($"http://localhost:{localPort}/api/status?devhostagentpodname={podName}");
                                if (routingStatusResponse == null)
                                {
                                    _log.Warning("Routing manager's GetStatus's RoutingStatusResponse is null which is expected.");
                                    return false;
                                }
                                routingStatus = await parseRoutingStatusFuncAsync(routingStatusResponse);
                                if (routingStatus == null)
                                {
                                    return false;
                                }
                                return routingStatus.IsConnected ?? true;
                            }
                            catch (HttpRequestException e)
                            {
                                _log.Warning("Routing manager's GetStatus threw HttpRequestException.");
                                _log.ExceptionAsWarning(e);
                                // Retry in case port forwarding hasn't been set up yet
                                return false;
                            }
                        },
                        maxWaitTime: TimeSpan.FromSeconds(15),
                        waitInterval: TimeSpan.FromSeconds(1),
                        cancellationToken: cancellationToken);

                        if (routingStatus == null)
                        {
                            _log.Warning("Routing manager's GetStatus: Current run failed, ran out of time.");
                            return null;
                        }
                        return routingStatus;
                    }
                    catch (TaskCanceledException e) when (e.InnerException.GetType() == typeof(IOException))
                    {
                        _log.Warning("Routing manager's GetStatus threw expected TaskCanceledException, Inner: IoException");
                        _log.ExceptionAsWarning(e);
                        return null;
                    }
                }
            };

            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                    Events.RoutingManagementClient.AreaName,
                    Events.RoutingManagementClient.Operations.GetStatus))
                    {
                        RoutingStatus routingStatus = null;
                        var getStatusResult = await WebUtilities.RetryUntilTimeWithWaitAsync(async _ =>
                        {
                            try
                            {
                                var localPort = PortManagementUtilities.GetAvailableLocalPort();
                                var routingManagerPod = await _kubernetesClient.GetFirstNamespacedPodWithLabelWithRunningContainerAsync(_namespaceName, Routing.RoutingManagerNameLower, new KeyValuePair<string, string>("routing.visualstudio.io/component", "routingmanager"), cancellationToken);

                                if (routingManagerPod == null)
                                {
                                    _log.Warning("Routing manager's GetStatus: Could not find a running routing manager pod");
                                    return false;
                                }

                                // Proxy to get the status of the pod that backs the routing manager service.
                                using (var manualResetEvent = new ManualResetEvent(false))
                                {
                                    using (var portForwardCancellationTokenSource = new CancellationTokenSource())
                                    {
                                        try
                                        {
                                            using (cancellationToken.Register(() => portForwardCancellationTokenSource.Cancel()))
                                            {
                                                isPortForwardSuccessful = false;
                                                Task.Run(() => startPortForwardToRoutingManager(routingManagerPod.Metadata.Name, localPort, manualResetEvent, portForwardCancellationTokenSource)).Forget();

                                                manualResetEvent.WaitOne(TimeSpan.FromSeconds(5));
                                                if (isPortForwardSuccessful)
                                                {
                                                    _log.Verbose("Port forward was successful. Trying to get status...");
                                                    routingStatus = await getRoutingStatusWithRetriesAsync(localPort, portForwardCancellationTokenSource.Token);
                                                }

                                                if (routingStatus == null)
                                                {
                                                    _log.Warning("Routing manager's GetStatus: Current run failed, ran out of time.");
                                                    return false;
                                                }
                                                // IsConnected is null when there is no entry for the devhostagent pod in status dictionary in routing manager yet
                                                else if (routingStatus.IsConnected == null)
                                                {
                                                    _log.Warning($"Routing manager's GetStatus: Routing wasn't setup properly on time: '{routingStatus.ErrorMessage ?? string.Empty}'");
                                                    return false;
                                                }

                                                return routingStatus.IsConnected ?? true;
                                            }
                                        }
                                        finally
                                        {
                                            portForwardCancellationTokenSource.Cancel();
                                        }
                                    }
                                }
                            }
                            catch (Exception e)
                            {
                                // Retry in case of all errors
                                _log.Warning("Error when trying to get status from routing manager");
                                _log.ExceptionAsWarning(e);
                                return false;
                            }
                        },
                        maxWaitTime: TimeSpan.FromMinutes(2),
                        waitInterval: TimeSpan.FromSeconds(2),
                        cancellationToken: cancellationToken);

                        if (!getStatusResult)
                        {
                            perfLogger.SetResult(OperationResult.Failed);
                            var errorString = routingStatus?.ErrorMessage ?? CommonResources.Error_OopsMessage;
                            _log.Error(Resources.FailedToGetRoutingManagerDeploymentStatusFormat, errorString);
                            return new OperationResponse<RoutingStatus>(new RoutingStatus(false, string.Format(Resources.FailedToGetRoutingManagerDeploymentStatusFormat, errorString)), _operationContext);
                        }

                        perfLogger.SetSucceeded();
                        return new OperationResponse<RoutingStatus>(routingStatus, _operationContext);
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig("Failed to get routing manager deployment status in namespace '{0}'.", _namespaceName));
        }

        /// <summary>
        /// <see cref="IRoutingManagementClient.GetValidationErrorsAsync"/>
        /// </summary>
        /// <returns></returns>
        public async Task<OperationResponse<string>> GetValidationErrorsAsync(string routeAsHeaderValue, CancellationToken cancellationToken)
        {
            var ingresses = await _kubernetesClient.ListIngressesInNamespaceAsync(_namespaceName, cancellationToken);
            foreach (var ingress in ingresses.Items)
            {
                // Ignore any ACME ingresses
                if (ingress.Spec.Rules?.Any(rule => !string.IsNullOrEmpty(rule?.Host) && rule?.Http?.Paths?.Any(path => !string.IsNullOrEmpty(path?.Path) && path.Path.Contains(Https.AcmePath)) == true) == true)
                {
                    continue;
                }
                // Check if the user is using letsencrypt
                if (ingress?.Metadata?.Annotations?.ContainsKey(Constants.Https.CertManagerAnnotationKey) == true
                    && StringComparer.OrdinalIgnoreCase.Equals(ingress.Metadata.Annotations[Constants.Https.CertManagerAnnotationKey], Constants.Https.LetsEncryptAnnotationValue)
                    // Check if https ingress is setup and Check if length of <routingheader>.<domainhost> is greater than 63
                    && ingress.Spec.Tls?.Any(tls => tls?.Hosts?.FirstOrDefault(host => host.Length + routeAsHeaderValue.Length + 1 > Constants.Https.LetsEncryptMaxDomainLength) != null) == true)
                {
                    return new OperationResponse<string>($"Isolation cannot be enabled as the resulting cloned ingress's host for the ingress {ingress.Metadata.Name} would be longer than {Constants.Https.LetsEncryptMaxDomainLength} characters, which isn't allowed by Let's Encrypt. Please reduce the length of your isolation header or your domain host.",
                        _operationContext);
                }
            }
            return new OperationResponse<string>(string.Empty, _operationContext);
        }

        #region Private methods

        /// <summary>
        /// Prepares the routing manager role spec.
        /// </summary>
        private V1Role PrepareV1Role()
        {
            V1Role role = new V1Role()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = RoutingManagerRoleName,
                    NamespaceProperty = _namespaceName,
                    Labels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                },
                Rules = new List<V1PolicyRule>()
                {
                    new V1PolicyRule()
                    {
                        ApiGroups = new List<string>() {""},
                        Resources = new List<string>() { "pods" },
                        Verbs = new List<string>() { "watch", "get", "list", "delete" }
                    },
                    new V1PolicyRule()
                    {
                        ApiGroups = new List<string>() {""},
                        Resources = new List<string>() { "services", "configmaps" },
                        Verbs = new List<string>() { "list", "create", "update", "delete" }
                    },
                    new V1PolicyRule()
                    {
                        ApiGroups = new List<string>() { "extensions", "networking.k8s.io" },
                        Resources = new List<string>() { "ingresses" },
                        Verbs = new List<string>() { "watch", "list", "create", "update", "delete" }
                    },
                    new V1PolicyRule()
                    {
                        ApiGroups = new List<string>() { "apps", "extensions" },
                        Resources = new List<string>() { "deployments", "deployments/status" },
                        Verbs = new List<string>() { "get", "list", "create", "update", "delete" }
                    },
                    new V1PolicyRule()
                    {
                        ApiGroups = new List<string>() { "traefik.containo.us" },
                        Resources = new List<string>() { "ingressroutes" },
                        Verbs = new List<string>() { "get", "create", "apply", "list", "delete", "update" }
                    }
                }
            };
            return role;
        }

        /// <summary>
        /// Prepares the routing manager service account spec.
        /// </summary>
        private V1ServiceAccount PrepareV1ServiceAccount()
        {
            V1ServiceAccount serviceAccount = new V1ServiceAccount()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = RoutingManagerServiceAccountName,
                    NamespaceProperty = _namespaceName,
                    Labels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                }
            };
            return serviceAccount;
        }

        /// <summary>
        // Prepares the cluster role binding spec for the routing manager service account.
        /// </summary>
        private V1RoleBinding PrepareV1RoleBinding()
        {
            V1RoleBinding roleBinding = new V1RoleBinding()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = RoutingManagerRoleBindingName,
                    NamespaceProperty = _namespaceName,
                    Labels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                },
                Subjects = new List<V1Subject>()
                {
                    new V1Subject()
                    {
                        Kind = "ServiceAccount",
                        Name = RoutingManagerServiceAccountName,
                        NamespaceProperty = _namespaceName
                    }
                },
                RoleRef = new V1RoleRef()
                {
                    Kind = "Role",
                    Name = RoutingManagerRoleName,
                    ApiGroup = "rbac.authorization.k8s.io"
                }
            };
            return roleBinding;
        }

        /// <summary>
        // Prepares the routing deployment spec.
        /// </summary>
        private V1Deployment PrepareV1Deployment()
        {
            V1Deployment deployment = new V1Deployment()
            {
                Metadata = new V1ObjectMeta
                {
                    Name = RoutingManagerDeploymentName,
                    NamespaceProperty = _namespaceName,
                    Labels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                },
                Spec = new V1DeploymentSpec
                {
                    Replicas = 1,
                    Selector = new V1LabelSelector()
                    {
                        MatchLabels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                    },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Labels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                        },
                        Spec = new V1PodSpec
                        {
                            Containers = new List<V1Container>()
                            {
                                new V1Container()
                                {
                                    Image = _imageProvider.Value.RoutingManagerImage,
                                    ImagePullPolicy = "Always",
                                    Name = Routing.RoutingManagerNameLower,
                                    Ports = new List<V1ContainerPort>()
                                    {
                                        new V1ContainerPort
                                        {
                                            ContainerPort = Routing.RoutingManagerPort
                                        }
                                    },
                                    Env = new List<V1EnvVar>()
                                    {
                                        new V1EnvVar("NAMESPACE", _namespaceName),
                                        new V1EnvVar(EnvironmentVariables.Names.CollectTelemetry, _environmentVariables.CollectTelemetry.ToString()),
                                        new V1EnvVar(EnvironmentVariables.Names.CorrelationId, _operationContext.CorrelationId)
                                    },
                                },
                            },
                            TerminationGracePeriodSeconds = 0,
                            ServiceAccountName = RoutingManagerServiceAccountName,
                            NodeSelector = new Dictionary<string, string>() { { KubernetesConstants.Labels.OS, KubernetesConstants.Labels.Values.Linux } }
                        }
                    }
                }
            };
            return deployment;
        }

        /// <summary>
        // Prepares the routing manager service spec.
        /// </summary>
        private V1Service PrepareV1Service()
        {
            V1Service service = new V1Service()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = Routing.RoutingManagerServiceName,
                    NamespaceProperty = _namespaceName,
                    Labels = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } }
                },
                Spec = new V1ServiceSpec()
                {
                    Selector = new Dictionary<string, string>() { { Routing.RoutingComponentLabel, Routing.RoutingManagerNameLower } },
                    Ports = new List<V1ServicePort>()
                    {
                        new V1ServicePort()
                        {
                            Protocol = "TCP",
                            Port = Routing.RoutingManagerPort,
                            TargetPort = Routing.RoutingManagerPort
                        }
                    }
                }
            };
            return service;
        }

        /// <summary>
        /// Check if Routing manager and any devhostagent pod is already running
        /// </summary>
        private async Task<bool> ShouldDeployRoutingManagerAsync(CancellationToken cancellationToken)
        {
            // Making the pods/deployment kubernetes calls sequentially because KubernetesClient on GKE doesn't handle refreshing tokens in parallel (https://github.com/kubernetes-client/csharp/issues/477).
            var podsInNamespace = await _kubernetesClient.ListPodsInNamespaceAsync(_namespaceName, cancellationToken: cancellationToken);
            var isAnyRemoteAgentPodRunning = podsInNamespace.Items.Any(pod => pod.Spec.Containers.Any(container => container.Image.Contains(_imageProvider.Value.DevHostImage)));
            if (!isAnyRemoteAgentPodRunning)
            {
                // If no remote agent pods are running, then we know we can safely deploy a new routing manager without risking to disrupt someone else.
                return true;
            }

            var deploymentsInNamespace = await _kubernetesClient.ListDeploymentsInNamespaceAsync(_namespaceName, cancellationToken);
            // Only checking for deployment object and not checking for other k8s objects that we deploy for routing manager. This level of check should be good enough for now.
            var isRoutingManagerPresent = deploymentsInNamespace.Items.Any(deploy => StringComparer.OrdinalIgnoreCase.Equals(deploy.Metadata.Name, RoutingManagerDeploymentName));
            // If some remote agent pods are running, only deploy the routing manager if it is missing (other devs using Bridge non-isolated).
            return !isRoutingManagerPresent;
        }

        #endregion Private methods
    }
}