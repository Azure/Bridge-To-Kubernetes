// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.Library.ServiceClients;
using Microsoft.BridgeToKubernetes.Library.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using static Microsoft.BridgeToKubernetes.Library.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Client.ManagementClients
{
    internal class KubernetesManagementClient : ManagementClientBase, IKubernetesManagementClient
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly KubernetesRestClientExceptionStrategy _kubernetesRestClientExceptionStrategy;
        private readonly ManagementClientExceptionStrategy _managementClientExceptionStrategy;
        private readonly IProgress<ProgressUpdate> _progress;

        public delegate KubernetesManagementClient Factory(string userAgent, string correlationId);

        public KubernetesManagementClient(
            string userAgent,
            string correlationId,
            IKubernetesClient kubernetesClient,
            KubernetesRestClientExceptionStrategy kubernetesRestClientExceptionStrategy,
            ManagementClientExceptionStrategy managementClientExceptionStrategy,
            IProgress<ProgressUpdate> progress,
            ILog log,
            IOperationContext operationContext)
            : base(log, operationContext)
        {
            this._kubernetesRestClientExceptionStrategy = kubernetesRestClientExceptionStrategy;
            this._managementClientExceptionStrategy = managementClientExceptionStrategy;
            this._kubernetesClient = kubernetesClient;
            this._progress = progress;

            var clientRequestId = string.IsNullOrEmpty(this._operationContext.ClientRequestId) ? Guid.NewGuid().ToString() : this._operationContext.ClientRequestId;

            _operationContext.UserAgent = userAgent;
            _operationContext.ClientRequestId = clientRequestId;
            _operationContext.CorrelationId = correlationId + LoggingConstants.CorrelationIdSeparator + LoggingUtils.NewId();
        }

        public async Task<bool> CheckCredentialsAsync(string targetNamespace, CancellationToken cancellationToken)
        {
            var result = false;
            await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                   Events.KubernetesManagementClient.AreaName,
                   Events.KubernetesManagementClient.Operations.CheckCredentialsAsync))
                    {
                        try
                        {
                            // If we are able to list pods using the the KubernetesClient everything is cool
                            var po = await _kubernetesClient.ListPodsInNamespaceAsync(targetNamespace);
                            perfLogger.SetSucceeded();
                            result = true;
                        }
                        catch (k8s.Exceptions.KubeConfigException ex)
                        {
                            _log.ExceptionAsWarning(ex);
                        }
                    }
                }, new ManagementClientExceptionStrategy.FailureConfig("Failed to Check Credentials"));
            return result;
        }

        public async Task RefreshCredentialsAsync(string targetNamespace, CancellationToken cancellationToken)
        {
            await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                   Events.KubernetesManagementClient.AreaName,
                   Events.KubernetesManagementClient.Operations.RefreshCredentialsAsync))
                    {
                        try
                        {
                            // If we are able to list pods using the the KubernetesClient everything is cool
                            var po = await _kubernetesClient.ListPodsInNamespaceAsync(targetNamespace);
                            perfLogger.SetSucceeded();
                            return;
                        }
                        catch (k8s.Exceptions.KubeConfigException ex)
                        {
                            _log.ExceptionAsWarning(ex);
                        }

                        string loginError = string.Empty;
                        // Let's try to start a long running kubectl command to log the user in and get credentials
                        Action<string> errorHandler = (string error) =>
                        {
                            var url = Regex.Match(error, @"(https://\S+)").Value;
                            var authenticationCode = Regex.Match(error, @"[A-Z0-9]{9}").Value;

                            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(authenticationCode))
                            {
                                // This usually happens after the first error message, after the user fails to login or if they don't have permissions to list namespaces
                                loginError = error;
                            }
                            else
                            {
                                // This usually happens for the first error message, where the user is asked to login
                                var authenticationTarget = new AuthenticationTarget
                                {
                                    Url = url,
                                    AuthenticationCode = authenticationCode
                                };
                                _progress.Report(new ProgressUpdate(20, ProgressStatus.None, new ProgressMessage(System.Diagnostics.Tracing.EventLevel.Informational, JsonHelpers.SerializeForLoggingPurpose(authenticationTarget))));
                            }
                        };
                        int exitCode = _kubernetesClient.InvokeLongRunningKubectlCommand(KubernetesCommandName.ListPods,
                        $"get po --namespace {targetNamespace}",
                        onStdErr: errorHandler,
                        shouldIgnoreErrors: true,
                        logOutput: false,
                        cancellationToken: cancellationToken);

                        if (exitCode != 0 || !string.IsNullOrEmpty(loginError))
                        {
                            throw new UserVisibleException(this._operationContext, "Unable to complete login: {0}", loginError);
                        }
                        perfLogger.SetSucceeded();
                    }
                }, new ManagementClientExceptionStrategy.FailureConfig("Failed to Refresh Credentials"));
        }

        public async Task<OperationResponse<IEnumerable<string>>> ListNamespacesAsync(CancellationToken cancellationToken, bool excludeReservedNamespaces = true)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                    Events.KubernetesManagementClient.AreaName,
                    Events.KubernetesManagementClient.Operations.ListNamespacesAsync))
                    {
                        V1NamespaceList result = await this._kubernetesRestClientExceptionStrategy.RunWithHandlingAsync(
                            async () =>
                            {
                                return await _kubernetesClient.ListNamespacesAsync(cancellationToken: cancellationToken);
                            },
                            new KubernetesRestClientExceptionStrategy.FailureConfig("Failed to list namespaces."));

                        IEnumerable<string> namespaces;
                        if (excludeReservedNamespaces)
                        {
                            namespaces = result.Items.Select(n => n.Metadata.Name).Except(Common.Kubernetes.KubernetesConstants.Namespaces.System);
                        }
                        else
                        {
                            namespaces = result.Items.Select(n => n.Metadata.Name);
                        }
                        perfLogger.SetSucceeded();
                        return new OperationResponse<IEnumerable<string>>(namespaces, _operationContext);
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig("Failed to list namespaces."));
        }

        public async Task<OperationResponse<IEnumerable<string>>> ListServicesInNamespacesAsync(string namespaceName, CancellationToken cancellationToken, bool excludeSystemServices = true)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
               async () =>
               {
                   using (var perfLogger = _log.StartPerformanceLogger(
                   Events.KubernetesManagementClient.AreaName,
                   Events.KubernetesManagementClient.Operations.ListServicesInNamespacesAsync))
                   {
                       V1ServiceList result = await this._kubernetesRestClientExceptionStrategy.RunWithHandlingAsync(
                           async () =>
                           {
                               return await _kubernetesClient.ListServicesInNamespaceAsync(namespaceName, cancellationToken: cancellationToken);
                           },
                           new KubernetesRestClientExceptionStrategy.FailureConfig("Failed to list services in namespace '{0}'.",
                                new PII(namespaceName)));

                       var serviceNames = result.Items.Select(s => s.Metadata.Name);
                       if (excludeSystemServices)
                       {
                           serviceNames = serviceNames.Where(name => !Common.Kubernetes.KubernetesConstants.ServiceNames.System.Any(
                               systemService => StringComparer.OrdinalIgnoreCase.Equals(systemService.serviceName, name) && StringComparer.OrdinalIgnoreCase.Equals(systemService.namespaceName, namespaceName)));
                       }
                       perfLogger.SetSucceeded();
                       return new OperationResponse<IEnumerable<string>>(serviceNames, _operationContext);
                   }
               },
               new ManagementClientExceptionStrategy.FailureConfig("Failed to list services in namespace '{0}'.",
                    new PII(namespaceName)));
        }

        public async Task<OperationResponse<IEnumerable<Uri>>> ListPublicUrlsInNamespaceAsync(string namespaceName, CancellationToken cancellationToken, string routingHeaderValue = null)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
               async () =>
               {
                   using (var perfLogger = _log.StartPerformanceLogger(
                   Events.KubernetesManagementClient.AreaName,
                   Events.KubernetesManagementClient.Operations.ListPublicUrlsInNamespaceAsync))
                   {
                       var urls = new List<Uri>();
                       urls.AddRange(await ListIngressUrlsAsync(namespaceName, cancellationToken, routingHeaderValue));
                       urls.AddRange(await ListLoadBalancerUrlsAsync(namespaceName, cancellationToken, routingHeaderValue));
                       perfLogger.SetSucceeded();
                       return new OperationResponse<IEnumerable<Uri>>(urls, _operationContext);
                   }
               },
               new ManagementClientExceptionStrategy.FailureConfig("Failed to list Urls in namespace '{0}'.", new PII(namespaceName)));
        }

        #region routing methods

        /// <summary>
        /// <see cref="IKubernetesManagementClient.GetRoutingHeader(CancellationToken)"/>
        /// </summary>
        public OperationResponse<string> GetRoutingHeader(CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                         Events.KubernetesManagementClient.AreaName,
                         Events.KubernetesManagementClient.Operations.GetRoutingHeaderValue))
            {
                var userName = RemoteEnvironmentUtilities.SanitizedUserName();
                var randomString = RemoteEnvironmentUtilities.RandomString(length: 4);
                if (userName.Length > 8)
                {
                    userName = userName.Substring(0, 8);
                }
                var operationResponse = $"{userName}-{randomString}";
                perfLogger.SetSucceeded();
                return new OperationResponse<string>(operationResponse, _operationContext);
            }
        }

        /// <summary>
        /// <see cref="IKubernetesManagementClient.IsRoutingSupportedAsync(CancellationToken)"/>
        /// </summary>
        public async Task<OperationResponse<bool>> IsRoutingSupportedAsync(CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                         Events.KubernetesManagementClient.AreaName,
                         Events.KubernetesManagementClient.Operations.IsRoutingSupported))
                    {
                        // Look for "azds" namespace in user cluster to determine if Dev Spaces is enabled.
                        V1NamespaceList namespaces;
                        try
                        {
                            namespaces = await _kubernetesClient.ListNamespacesAsync(cancellationToken: cancellationToken);
                        }
                        catch (Exception e)
                        {
                            // If we fail to enumerate the namespaces, we assume the "azds" namespace is not present.
                            _log.ExceptionAsWarning(e);
                            return new OperationResponse<bool>(true, _operationContext);
                        }
                        var azdsNamespaceExists = namespaces.Items.Any(ns => StringComparer.OrdinalIgnoreCase.Equals(ns.Metadata.Name, AzureDevSpacesService.UserClusterNamespaceName));
                        perfLogger.SetSucceeded();
                        return new OperationResponse<bool>(!azdsNamespaceExists, _operationContext);
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig("Failed to determine if routing is supported."));
        }

        #endregion routing methods

        #region private methods

        private async Task<IEnumerable<Uri>> ListIngressUrlsAsync(string namespaceName, CancellationToken cancellationToken, string routingHeaderValue = null)
        {
            var ingresses = await this._kubernetesRestClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    return await _kubernetesClient.ListIngressesInNamespaceAsync(namespaceName, cancellationToken: cancellationToken);
                },
                new KubernetesRestClientExceptionStrategy.FailureConfig("Failed to list ingresses in namespace '{0}'.", new PII(namespaceName)));

            // Exclude ingresses generated by our tooling
            var userIngresses = ingresses?.Items?.Where(i => i.Metadata.Labels == null || !i.Metadata.Labels.ContainsKey(Routing.GeneratedLabel));
            return ResolveIngressesUrls(userIngresses, cancellationToken, routingHeaderValue);
        }

        private async Task<IEnumerable<Uri>> ListLoadBalancerUrlsAsync(string namespaceName, CancellationToken cancellationToken, string routingHeaderValue = null)
        {
            var loadBalancers = await this._kubernetesRestClientExceptionStrategy.RunWithHandlingAsync(
            async () =>
            {
                return await _kubernetesClient.ListLoadBalancerServicesInNamespaceAsync(namespaceName, cancellationToken);
            },
            new KubernetesRestClientExceptionStrategy.FailureConfig("Failed to list load balancer services in namespace '{0}'.", new PII(namespaceName)));

            var loadBalancerUrls = new List<Uri>();
            foreach (var loadBalancer in loadBalancers)
            {
                var loadBalancerIngress = loadBalancer?.Status?.LoadBalancer?.Ingress?.FirstOrDefault();

                string host = string.Empty;
                try
                {
                    if (!string.IsNullOrWhiteSpace(loadBalancerIngress?.Ip))
                    {
                        host =
                            string.IsNullOrWhiteSpace(routingHeaderValue) ?
                                $"http://{loadBalancerIngress.Ip}"
                                : $"http://{routingHeaderValue}.{loadBalancerIngress.Ip}.nip.io";
                        loadBalancerUrls.Add(new Uri(host));
                    }

                    if (!string.IsNullOrWhiteSpace(loadBalancerIngress?.Hostname))
                    {
                        host =
                            string.IsNullOrWhiteSpace(routingHeaderValue) ?
                                $"http://{loadBalancerIngress.Hostname}"
                                : $"http://{routingHeaderValue}.{loadBalancerIngress.Hostname}";
                        loadBalancerUrls.Add(new Uri(host));
                    }
                }
                catch (UriFormatException e)
                {
                    _log.Warning("Failed to create URL for host '{0}': {1}", host, e.Message);
                }
            }
            return loadBalancerUrls;
        }

        /// <summary>
        /// Convert Kubernetes <see cref="V1Ingress"/> objects into <see cref="Uri"/> , optionally filtered to a single <paramref name="targetSpaceName"/>
        /// </summary>
        /// <param name="k8sIngresses">List of ingresses in v1.16 and higher.</param>
        /// <param name="routingHeaderValue">When specified, prefix ingress uris with this routing header.</param>
        private IEnumerable<Uri> ResolveIngressesUrls(IEnumerable<V1Ingress> k8sIngresses, CancellationToken cancellationToken, string routingHeaderValue = null)
        {
            if (k8sIngresses == null)
            {
                return null;
            }

            var result = new List<Uri>();
            k8sIngresses.ExecuteForEach(k8sIngress =>
            {
                if (k8sIngress?.Spec?.Rules == null)
                {
                    return;
                }

                k8sIngress.Spec.Rules.ExecuteForEach(rule =>
                {
                    if (rule?.Host == null || rule?.Http?.Paths == null)
                    {
                        return;
                    }

                    var usesHttps = k8sIngress?.Spec?.Tls?.Any(tls => tls?.Hosts?.Any(host => StringComparer.OrdinalIgnoreCase.Equals(host, rule.Host)) == true) == true;
                    var protocol = usesHttps ? "https://" : "http://";

                    rule.Http.Paths.ExecuteForEach(path =>
                    {
                        if (path?.Backend?.Service?.Name == null)
                        {
                            return;
                        }

                        // empty path should default to "/"
                        if (path.Path == null)
                        {
                            path.Path = "/";
                        }

                        // Check if the path is for an ACME challenge in case of HTTPS let's encrypt
                        if (path.Path.Contains(Common.Constants.Https.AcmePath))
                        {
                            return;
                        }

                        var host = string.IsNullOrEmpty(routingHeaderValue) ? $"{protocol}{rule.Host}" : $"{protocol}{routingHeaderValue}.{rule.Host}";

                        try
                        {
                            result.Add(new Uri(new Uri(host), path.Path));
                        }
                        catch (UriFormatException e)
                        {
                            _log.Warning("Failed to create URL for host '{0}' and relativePath '{1}': {2}", host, path.Path, e.Message);
                        }
                    }, cancellationToken);
                }, cancellationToken);
            }, cancellationToken);
            return result;
        }
    }

    # endregion private methods
}