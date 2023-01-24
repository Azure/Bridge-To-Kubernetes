// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.RoutingManager.Configuration;
using Microsoft.BridgeToKubernetes.RoutingManager.Logging;
using Microsoft.BridgeToKubernetes.RoutingManager.Traefik;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    /// <summary>
    /// This class captures the logic of Routing Manager.
    /// Please refer to the readme for its responsibilities.
    /// </summary>
    internal class RoutingManagerApp : IHostedService
    {
        private ILog _log;
        private readonly IRoutingManagerConfig _routingManagerConfig;
        private IKubernetesWatcher _kubernetesWatcher;
        private IKubernetesClient _kubernetesClient;
        private Func<IEnumerable<V1Service>, IEnumerable<V1Deployment>, IEnumerable<V1ConfigMap>, IEnumerable<V1Ingress>, IEnumerable<IngressRoute>, RoutingStateEstablisher> _routingStateEstablisherFactory;
        private AutoResetEvent _trigger;
        private volatile object _recentEvent;
        private DateTime _lastRefreshTime = DateTime.Now;

        public static (IDictionary<string, string> EntityTriggerNamesStatus, string RoutingErrorMessage) Status = (new Dictionary<string, string>(), string.Empty);

        public RoutingManagerApp(
            ILog log,
            IKubernetesClient kubernetesClient,
            IKubernetesWatcher kubernetesWatcher,
            IRoutingManagerConfig routingManagerConfig,
            Func<IEnumerable<V1Service>, IEnumerable<V1Deployment>, IEnumerable<V1ConfigMap>, IEnumerable<V1Ingress>, IEnumerable<IngressRoute>, RoutingStateEstablisher> routingStateEstablisherFactory)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _kubernetesWatcher = kubernetesWatcher ?? throw new ArgumentNullException(nameof(kubernetesWatcher));
            _routingManagerConfig = routingManagerConfig ?? throw new ArgumentNullException(nameof(routingManagerConfig));
            _routingStateEstablisherFactory = routingStateEstablisherFactory ?? throw new ArgumentNullException(nameof(routingStateEstablisherFactory));
        }

        /// <summary>
        /// Start watching kubernetes objects
        /// </summary>
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _log.Info("App started with version : '{0}'", Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);

            // Initialize the auto reset event
            _trigger = new AutoResetEvent(false);

            using (var watchCts = new CancellationTokenSource())
            using (cancellationToken.Register(() => watchCts.Cancel()))
            {
                // Watch objects
                var podWatchTask = _kubernetesWatcher.WatchPodsAsync(
                    _routingManagerConfig.GetNamespace(),
                    (type, meta) =>
                    {
                        if (!meta.IsRoutingTrigger())
                        {
                            // Ignore generated service objects and service objects without
                            // the specific label and annotation we are looking for
                            return;
                        }
                        _log.Info("Pod '{0}.{1}' was '{2}'", new PII(meta.Name), new PII(meta.NamespaceProperty), type.ToString());
                        OnWatchEvent();
                    },
                    watchCts.Token);
                var ingressWatchTask = _kubernetesWatcher.WatchIngressesAsync(
                    _routingManagerConfig.GetNamespace(),
                    (type, meta) =>
                    {
                        if (meta.IsGenerated())
                        {
                            // Ignore generated ingress objects
                            return;
                        }
                        _log.Info("Ingress '{0}.{1}' was '{2}'", new PII(meta.Name), new PII(meta.NamespaceProperty), type.ToString());
                        OnWatchEvent();
                    },
                    watchCts.Token);

                // Refresh as objects change, until cancelled
                RefreshLoopAsync(cancellationToken).Wait();

                // Cancel the watches and wait for them to shut down
                watchCts.Cancel();
                Task.WaitAll(podWatchTask, ingressWatchTask);

                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// Stop watching kubernetes objects
        /// </summary>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async void OnWatchEvent()
        {
            // Create a unique object representing this event
            var ev = new object();

            // Save this object as the most recent event
            _recentEvent = ev;

            try
            {
                // Wait for 1 second
                await Task.Delay(1000);
            }
            catch (TaskCanceledException)
            {
                //ignore task cancellation exception
                _log.Info("Routing manager cancelled before kubernetes event was handled.");
                return;
            }

            // If this is still the most recent event, it's time to refresh.
            // This waiting and checking is designed to minimize the number
            // of times the refresh procedure is run if a number of objects
            // are changed in a short period of time (less than one second).
            // Force a refresh every 10 seconds in case continual events
            // are preventing the refresh from ever running.
            if (_recentEvent == ev || (DateTime.Now - _lastRefreshTime) > TimeSpan.FromSeconds(10))
            {
                _lastRefreshTime = DateTime.Now;

                // Signal the trigger to kick off the refresh. If a refresh
                // is currently in progress, that refresh will complete, then
                // it will immediately refresh again. This should ensure that
                // the latest object changes are always accounted for.
                _trigger.Set();
            }
        }

        /// <summary>
        /// Continuously running loop that lists kubernetes objects from cluster
        /// </summary>
        private async Task RefreshLoopAsync(CancellationToken cancellationToken)
        {
            while (true)
            {
                using (var perfLogger = _log.StartPerformanceLogger(Events.RoutingManager.AreaName, Events.RoutingManager.Operations.RefreshLoop))
                {
                    // Wait until there are changes to objects
                    WaitHandle.WaitAny(new WaitHandle[] { _trigger, cancellationToken.WaitHandle });

                    // If cancellation was requested, stop
                    if (cancellationToken.IsCancellationRequested)
                    {
                        perfLogger.SetCancelled();
                        break;
                    }

                    // Please don't leak memory.
                    // Added this comment for good luck.
                    using (var internalCancellationTokenSource = new CancellationTokenSource())
                    using (cancellationToken.Register(() => internalCancellationTokenSource.Cancel()))
                    {
                        try
                        {
                            _log.Info("Refreshing objects...");
                            await RefreshAsync(internalCancellationTokenSource.Token);
                            _log.Info("Refreshed objects");
                            perfLogger.SetSucceeded();
                        }
                        catch (OperationCanceledException)
                        {
                            // Expected, non-erroneous exception
                            perfLogger.SetCancelled();
                        }
                        catch (HttpRequestException ex)
                        {
                            // User cluster cannot be reached
                            _log.ExceptionAsWarning(ex);
                            perfLogger.SetResult(OperationResult.Failed);
                        }
                        catch (Exception ex)
                        {
                            // Write exception for diagnostic purposes
                            _log.Exception(ex);
                            perfLogger.SetResult(OperationResult.Failed);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Refresh method - creates the pod triggers and ingress triggers to then
        /// pass to <see cref="RoutingStateEstablisher"/> for processing
        /// </summary>
        /// <param name="cancellationToken"></param>
        private async Task RefreshAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Get the current state of all ingress and service objects
                var (ingresses, services, deployments, configmaps, pods, ingressRoutes) = await GetObjectsAsync(cancellationToken);
                var userIngresses = ingresses.Generated(false);
                var userServices = services.Generated(false);
                var userIngressRoutes = ingressRoutes.Generated(false);
                _log.Verbose($"{userIngresses.Count()} user ingresses found");

                var routingStateEstablisherInputMap = new ConcurrentDictionary<V1Service, RoutingStateEstablisherInput>();
                using (var perfLogger = _log.StartPerformanceLogger(Events.RoutingManager.AreaName, Events.RoutingManager.Operations.CreateTriggers))
                {
                    await Task.WhenAll(
                        AddPodTriggersAsync(routingStateEstablisherInputMap, pods, userServices, cancellationToken),
                        AddIngressTriggersAsync(routingStateEstablisherInputMap, userIngresses, userServices, pods, cancellationToken),
                        AddLoadBalancerTriggersAsync(routingStateEstablisherInputMap, userServices, pods, cancellationToken),
                        AddIngressRouteTriggersAsync(routingStateEstablisherInputMap, userIngressRoutes, userServices, pods, cancellationToken));

                    _log.Info("Created '{0}' pod triggers, '{1}' ingress triggers, '{2}' ingressRoute triggers and '{3}' load balancer triggers",
                        routingStateEstablisherInputMap.Select(input => input.Value.PodTriggers.Count()).Sum(),
                        routingStateEstablisherInputMap.Select(input => input.Value.IngressTriggers.Count()).Sum(),
                        routingStateEstablisherInputMap.Select(input => input.Value.IngressRouteTriggers.Count()).Sum(),
                        routingStateEstablisherInputMap.Count(input => input.Value.LoadBalancerTrigger != null));
                    var inputKeys = routingStateEstablisherInputMap.Keys as IList<V1Service>;
                    var inputValues = routingStateEstablisherInputMap.Values as IList<RoutingStateEstablisherInput>;
                    for (int i = 0; i < routingStateEstablisherInputMap.Count(); i++)
                    {
                        _log.Verbose("Routing state establisher input {0} Key : service named '{1}' in namespace '{2}'", i, new PII(inputKeys[i].Metadata.Name), new PII(inputKeys[i].Metadata.NamespaceProperty));
                        _log.Verbose("Routing state establisher input {0} Value : '{1}'", i, new PII(JsonSerializer.Serialize(inputValues[i])));
                    }
                    perfLogger.SetSucceeded();
                }

                // Call stateEstablisher
                var routingStateEstablisher = _routingStateEstablisherFactory(services, deployments, configmaps, ingresses, ingressRoutes);
                Status.EntityTriggerNamesStatus = await routingStateEstablisher.RunAsync(routingStateEstablisherInputMap, cancellationToken);
                _log.Info("Completed setting up routing in the namespace '{0}'", new PII(_routingManagerConfig.GetNamespace()));
            }
            catch (RoutingException e)
            {
                // We do not throw the exception so that the pod does not recycle and the exception message can be fetched
                _log.Exception(e);
                Status.RoutingErrorMessage = e.Message;
            }
        }

        /// <summary>
        /// List various Kubernetes objects from the cluster
        /// </summary>
        private async Task<(IEnumerable<V1Ingress>, IEnumerable<V1Service>, IEnumerable<V1Deployment>, IEnumerable<V1ConfigMap>, IEnumerable<V1Pod>, IEnumerable<IngressRoute>)> GetObjectsAsync(CancellationToken cancellationToken)
        {
            // Retrieve ingresses and services in parallel
            var ingresses = GetIngressesAsync(cancellationToken);
            var services = GetServicesAsync(cancellationToken);
            var deployments = GetDeploymentsAsync(cancellationToken);
            var configMaps = GetConfigMapsAsync(cancellationToken);
            var pods = GetPodsAsync(cancellationToken);
            var ingressRoutes = GetIngressRoutesAsync(cancellationToken);
            // bug 1006600
            await Task.WhenAll(ingresses, services, deployments, configMaps, pods, ingressRoutes);
            return (ingresses.Result, services.Result, deployments.Result, configMaps.Result, pods.Result, ingressRoutes.Result);
        }

        private async Task AddIngressTriggersAsync(
            ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> routingStateEstablisherInputMap,
            IEnumerable<V1Ingress> userIngresses,
            IEnumerable<V1Service> userServices,
            IEnumerable<V1Pod> pods,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var ingress in userIngresses)
            {
                _log.Info("Processing the ingress named '{0}'", new PII(ingress.Metadata.Name));
                if (string.IsNullOrWhiteSpace(ingress?.Metadata?.Name) || string.IsNullOrWhiteSpace(ingress?.Metadata?.NamespaceProperty))
                {
                    _log.Warning("Encountered ingress with null/empty name or namespace property");
                    continue;
                }

                var isAgicIngress = ingress.IsAgicIngress();
                _log.Verbose("Is Agic ingress: {0}", isAgicIngress);
                _log.Verbose("Agic backend hostname found : '{0}'", ingress.TryGetAgicBackendHostnameAnnotation(_log, out string agicBackendHostnameAnnotationValue));

                var rules = ingress.Spec?.Rules;
                if (rules == null)
                {
                    // Nothing to do
                    _log.Warning("No rules for ingress '{0}' in namespace '{1}'", new PII(ingress.Metadata.Name), new PII(ingress.Metadata.NamespaceProperty));
                    continue;
                }
                if (rules.Any(rule => !string.IsNullOrEmpty(rule.Host) && rule.Http?.Paths != null && rule.Http.Paths.Any(path => !string.IsNullOrEmpty(path.Path) && path.Path.Contains(Common.Constants.Https.AcmePath))))
                {
                    // https://letsencrypt.org/docs/challenge-types/
                    _log.Verbose("Ignoring this ingress since it is an ACME HTTP01 challenge ingress");
                    continue;
                }

                (var httpReadinessProbe, var httpLivenessProbe) = await GetHttpProbesRunningUnderIngressAsync(ingress, userServices, pods, cancellationToken);

                foreach (var rule in rules)
                {
                    var paths = rule.Http?.Paths;
                    if (paths == null)
                    {
                        // Nothing to do
                        _log.Warning("No paths on rule for ingress '{0}' in namespace '{1}'", new PII(ingress.Metadata.Name), new PII(ingress.Metadata.NamespaceProperty));
                        continue;
                    }
                    foreach (var path in paths)
                    {
                        var serviceName = path?.Backend?.Service?.Name;
                        string servicePort = path?.Backend?.Service?.Port?.Name;
                        if (string.IsNullOrEmpty(servicePort)) {
                            servicePort = path?.Backend?.Service?.Port?.Number.ToString();
                            _log.Info("service port name not found for ingress, port number is {0}", servicePort);
                        }
                        if (!string.IsNullOrWhiteSpace(serviceName))
                        {
                            V1Service serviceToAdd;
                            if ((serviceToAdd = userServices.FirstOrDefault(svc => StringComparer.OrdinalIgnoreCase.Equals(svc.Metadata.Name, serviceName))) == default(V1Service))
                            {
                                //  If we do not find the service in the same namespace, this ingress is broken.
                                _log.Warning("Service '{0}' was not found which was expected to be trigger service. Skipping this ingress trigger named '{1}' with host '{2}' since it is invalid.",
                                    new PII(serviceName), new PII(ingress.Metadata.Name), new PII(rule.Host ?? string.Empty));
                                continue;
                            }

                            string serviceProtocol;
                            int servicePort_int;
                            try
                            {
                                (serviceProtocol, servicePort_int) = await GetProtocolAndPortNumberFromServiceNamedPort(serviceToAdd, servicePort, ingress.Metadata.Name, cancellationToken);
                            }
                            catch (InvalidOperationException)
                            {
                                _log.Error("Unable to retrieve the integer port for the named port '{0}' for service '{1}' in ingress '{2}'. Ignoring corresponding ingress trigger with host '{3}'",
                                    new PII(servicePort), new PII(serviceName), new PII(ingress.Metadata.Name), new PII(string.IsNullOrWhiteSpace(rule.Host) ? string.Empty : rule.Host));
                                continue;
                            }
                            catch (RoutingException)
                            {
                                _log.Info("Ignoring service path '{0}' in ingress '{1}'", new PII(path.Backend.Service.Name), new PII(ingress.Metadata.Name));
                                continue;
                            }

                            if (!StringComparer.OrdinalIgnoreCase.Equals(serviceProtocol, KubernetesConstants.Protocols.Tcp))
                            {
                                _log.Error("Detected service protocol '{0}'. Routing manager does not support protocols other than TCP. Ignoring corresponding ingress trigger named '{1}' with host '{2}'",
                                    serviceProtocol, new PII(ingress.Metadata.Name), new PII(string.IsNullOrWhiteSpace(rule.Host) ? string.Empty : rule.Host));
                                continue;
                            }

                            if (!await ReplaceServiceNamedPortsAsync(serviceToAdd, pods, cancellationToken))
                            {
                                _log.Warning("'{0}' Trigger service's underlying pods or pod's corresponding port was not found in order to resolve named target port. Ignoring corresponding ingress trigger named '{1}'",
                                        new PII(serviceToAdd.Metadata.Name), new PII(ingress.Metadata.Name));
                                continue;
                            }

                            var ingressTriggerToAdd =
                                new IngressTriggerConfig(
                                    namespaceName: ingress.Metadata.NamespaceProperty,
                                    triggerService: serviceToAdd,
                                    ingressName: ingress.Metadata.Name,
                                    servicePort: servicePort_int,
                                    host: rule.Host,
                                    isAgicIngress: isAgicIngress,
                                    agicBackendHostname: agicBackendHostnameAnnotationValue,
                                    httpReadinessProbe: httpReadinessProbe,
                                    httpLivenessProbe: httpLivenessProbe);
                            routingStateEstablisherInputMap.AddOrUpdateWithTrigger(serviceToAdd, ingressTriggerToAdd);
                        }
                    }
                }
            }
        }

        private async Task AddIngressRouteTriggersAsync(
            ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> routingStateEstablisherInputMap,
            IEnumerable<IngressRoute> userIngressRoutes,
            IEnumerable<V1Service> userServices,
            IEnumerable<V1Pod> pods,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var ingressRoute in userIngressRoutes)
            {
                _log.Info("Processing the ingressRoute named '{0}'", new PII(ingressRoute.Metadata.Name));
                if (string.IsNullOrWhiteSpace(ingressRoute?.Metadata?.Name) || string.IsNullOrWhiteSpace(ingressRoute?.Metadata?.NamespaceProperty))
                {
                    _log.Warning("Encountered ingressRoute with null/empty name or namespace property");
                    continue;
                }

                var routes = ingressRoute.Spec?.Routes;
                if (routes == null)
                {
                    // Nothing to do
                    _log.Warning("No routes for ingressRoute '{0}' in namespace '{1}'", new PII(ingressRoute.Metadata.Name), new PII(ingressRoute.Metadata.NamespaceProperty));
                    continue;
                }

                _log.Info(JsonSerializer.Serialize(routes));
                (var httpReadinessProbe, var httpLivenessProbe) = await GetHttpProbesForServiceNamesAsync("ingressRoute", ingressRoute.Metadata.Name, userServices, pods, routes.SelectMany(r => r.Services == null ? new List<string>() : r.Services.Select(s => s.Name)), cancellationToken);
                foreach (var route in routes)
                {
                    if (route.Services == null || !route.Services.Any())
                    {
                        _log.Warning("No services found for the ingressRoute {0} for the match {1} in namespace {2}", new PII(ingressRoute.Metadata.Name), new PII(route.Match), new PII(ingressRoute.Metadata.NamespaceProperty));
                        continue;
                    }

                    foreach (var ingressRouteService in route.Services)
                    {
                        V1Service serviceToAdd;
                        if ((serviceToAdd = userServices.FirstOrDefault(svc => StringComparer.OrdinalIgnoreCase.Equals(svc.Metadata.Name, ingressRouteService.Name))) == default(V1Service))
                        {
                            //  If we do not find the service in the same namespace, this ingress is broken.
                            _log.Warning("Service '{0}' was not found which was expected to be trigger service. Skipping this ingressRoute trigger with match '{1}'.",
                                new PII(ingressRouteService.Name), new PII(route.Match));
                            continue;
                        }

                        string serviceProtocol;
                        int servicePort_int;
                        try
                        {
                            (serviceProtocol, servicePort_int) = await GetProtocolAndPortNumberFromServiceNamedPort(serviceToAdd, ingressRouteService.Port, ingressRoute.Metadata.Name, cancellationToken);
                        }
                        catch (InvalidOperationException)
                        {
                            _log.Error("Unable to retrieve the integer port for the named port '{0}' for service '{1}' in ingressRoute '{2}'. Ignoring corresponding ingressRoute trigger with match '{3}'",
                                new PII(ingressRouteService.Port.ToString()), new PII(ingressRouteService.Name), new PII(ingressRoute.Metadata.Name), new PII(route.Match));
                            continue;
                        }
                        catch (RoutingException)
                        {
                            // Ignore this ingress route
                            _log.Info("Ignoring ingressroute service '{0}'", new PII(ingressRouteService.Name));
                            continue;
                        }

                        if (!StringComparer.OrdinalIgnoreCase.Equals(serviceProtocol, KubernetesConstants.Protocols.Tcp))
                        {
                            _log.Error("Detected service protocol '{0}'. Routing manager does not support protocols other than TCP. Ignoring corresponding ingressRoute trigger named '{1}' with match '{2}'",
                                serviceProtocol, new PII(ingressRoute.Metadata.Name), new PII(route.Match));
                            continue;
                        }

                        if (!await ReplaceServiceNamedPortsAsync(serviceToAdd, pods, cancellationToken))
                        {
                            _log.Warning("'{0}' Trigger service's underlying pods or pod's corresponding port was not found in order to resolve named target port. Ignoring corresponding ingressRoute trigger named '{1}'",
                                    new PII(serviceToAdd.Metadata.Name), new PII(ingressRoute.Metadata.Name));
                            continue;
                        }

                        var ingressRouteTriggerToAdd =
                                new IngressRouteTriggerConfig(
                                    namespaceName: ingressRoute.Metadata.NamespaceProperty,
                                    triggerService: serviceToAdd,
                                    ingressRouteName: ingressRoute.Metadata.Name,
                                    servicePort: servicePort_int,
                                    host: ingressRoute.GetIngressRouteHostForRoute(route).First(),
                                    httpReadinessProbe: httpReadinessProbe,
                                    httpLivenessProbe: httpLivenessProbe);
                        routingStateEstablisherInputMap.AddOrUpdateWithTrigger(serviceToAdd, ingressRouteTriggerToAdd);
                    }
                }
            }
        }

        private Task<(V1Probe, V1Probe)> GetHttpProbesRunningUnderIngressAsync(
            V1Ingress ingress,
            IEnumerable<V1Service> allServices,
            IEnumerable<V1Pod> allPods,
            CancellationToken cancellationToken)
        {
            var serviceNames = new List<string>();
            V1Probe httpReadinessProbe = null;
            V1Probe httpLivenessProbe = null;
            if (ingress.Spec.Rules == null)
            {
                return Task.FromResult((httpReadinessProbe, httpLivenessProbe));
            }

            foreach (var rule in ingress.Spec.Rules)
            {
                cancellationToken.ThrowIfCancellationRequested();
                foreach (var path in rule.Http.Paths)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!string.IsNullOrWhiteSpace(path?.Backend?.Service?.Name))
                    {
                        serviceNames.Add(path.Backend.Service?.Name);
                    }
                }
            }
            return GetHttpProbesForServiceNamesAsync("ingress", ingress.Metadata.Name, allServices, allPods, serviceNames, cancellationToken);
        }

        private async Task<(V1Probe, V1Probe)> GetHttpProbesForServiceNamesAsync(
            string resourceType,
            string resourceName,
            IEnumerable<V1Service> allServices,
            IEnumerable<V1Pod> allPods,
            IEnumerable<string> serviceNames,
            CancellationToken cancellationToken)
        {
            var servicesUnderIngressRoute = new List<V1Service>();
            V1Probe httpReadinessProbe = null;
            V1Probe httpLivenessProbe = null;

            await serviceNames.ExecuteForEachAsync(serviceName =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                V1Service retrievedService = allServices.FirstOrDefault(svc => StringComparer.OrdinalIgnoreCase.Equals(svc.Metadata.Name, serviceName));
                if (retrievedService != null)
                {
                    servicesUnderIngressRoute.Add(retrievedService);
                }
                return Task.CompletedTask;
            });

            foreach (var service in servicesUnderIngressRoute)
            {
                cancellationToken.ThrowIfCancellationRequested();
                V1Pod selectedPod = null;
                // For all runs after the first one, we need to rely on the original service selector annotation because we modify the user's service's label selectors
                if (service.Metadata.Annotations?.ContainsKey(Constants.OriginalServiceSelectorAnnotation) == true)
                {
                    selectedPod = allPods.FirstOrDefault(pod => IsMatchSelectorsWithLabels(service.Metadata.GetOriginalServiceSelectors(), pod.Metadata.Labels));
                }
                else
                {
                    selectedPod = allPods.FirstOrDefault(pod => IsMatchSelectorsWithLabels(service.Spec.Selector, pod.Metadata.Labels));
                }
                if (selectedPod == null)
                {
                    continue;
                }

                var firstContainerInPod = selectedPod.Spec.Containers.FirstOrDefault();
                httpLivenessProbe = firstContainerInPod?.LivenessProbe;
                httpReadinessProbe = firstContainerInPod?.ReadinessProbe;

                if (httpLivenessProbe != null && httpLivenessProbe != null)
                {
                    _log.Info("Extracted probes for envoy pod under the '{0}' '{1}'", resourceType, new PII(resourceName));
                    if (httpLivenessProbe?.HttpGet?.Port?.Value != null
                    && !int.TryParse(httpLivenessProbe.HttpGet.Port.Value, out _))
                    {
                        httpLivenessProbe.HttpGet.Port = GetIntValueOfNamedPortFromContainer(firstContainerInPod, httpLivenessProbe.HttpGet.Port.Value);
                    }

                    if (httpReadinessProbe?.HttpGet?.Port?.Value != null
                        && !int.TryParse(httpReadinessProbe.HttpGet.Port.Value, out _))
                    {
                        httpReadinessProbe.HttpGet.Port = GetIntValueOfNamedPortFromContainer(firstContainerInPod, httpReadinessProbe.HttpGet.Port.Value);
                    }
                    break;
                }
            }

            return (httpLivenessProbe, httpReadinessProbe);
        }

        private int GetIntValueOfNamedPortFromContainer(V1Container container, string namedPort)
        {
            if (container?.Ports == null || string.IsNullOrEmpty(namedPort))
            {
                _log.Error("Unable to find named port '{0}' in container '{1}' - either container ports are empty or named port is empty. ", new PII(namedPort), new PII(container.Name));
                throw new RoutingException(Resources.FailedToFindNamedPortEmptyFormat, namedPort, container.Name);
            }
            foreach (var port in container.Ports)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(port.Name, namedPort))
                {
                    return port.ContainerPort;
                }
            }

            _log.Error("Unable to find named port '{0}' in container '{1}'. ", new PII(namedPort), new PII(container.Name));
            throw new RoutingException(Resources.FailedToFindNamedPortFormat, namedPort, container.Name);
        }

        private bool IsMatchSelectorsWithLabels(IDictionary<string, string> selectors, IDictionary<string, string> labels)
        {
            return (labels == null && selectors == null)
                || (labels != null && selectors != null && selectors.All(selector => labels.TryGetValue(selector.Key, out string labelValue) && StringComparer.OrdinalIgnoreCase.Equals(labelValue, selector.Value)));
        }

        private async Task AddPodTriggersAsync(
            ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> routingStateEstablisherInputMap,
            IEnumerable<V1Pod> pods,
            IEnumerable<V1Service> userServices,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var pod in pods)
            {
                if (pod.Metadata.IsRoutingTrigger())
                {
                    _log.Info("Pod '{0}' is a pod trigger", new PII(pod.Metadata.Name));
                    var routeOnHeader = pod.Metadata.GetRouteOnHeader(_log);
                    var routeFromServiceName = pod.Metadata.GetRouteFromServiceName(_log);
                    var triggerService = userServices.FirstOrDefault(svc => StringComparer.OrdinalIgnoreCase.Equals(svc.Metadata.Name, routeFromServiceName));
                    if (triggerService == default(V1Service))
                    {
                        _log.Error("Service '{0}' not found which was expected to be trigger service. Skipping this pod trigger named '{1}'", new PII(routeFromServiceName), new PII(pod.Metadata.Name));
                        continue;
                    }

                    V1Pod pod_latest = null;
                    if (string.IsNullOrWhiteSpace(pod.Status.PodIP))
                    {
                        await WebUtilities.RetryUntilTimeAsync(async _ =>
                        {
                            pod_latest = await _kubernetesClient.GetV1PodAsync(pod.Metadata.NamespaceProperty, pod.Metadata.Name, cancellationToken: cancellationToken);
                            if (pod_latest == null || pod_latest.Status == null || string.IsNullOrWhiteSpace(pod_latest.Status.PodIP))
                            {
                                return false;
                            }
                            return true;
                        },
                        maxWaitTime: TimeSpan.FromSeconds(5),
                        cancellationToken: cancellationToken);
                    }
                    if (pod_latest != null && string.IsNullOrEmpty(pod_latest.Status.PodIP))
                    {
                        _log.Error("Could not retrive IP for pod trigger : '{0}', skipping this trigger", new PII(pod_latest.Metadata.Name));
                        continue;
                    }

                    _log.Info("Retrieved pod ip for pod trigger '{0}'", new PII(pod.Metadata.Name));

                    if (!await ReplaceServiceNamedPortsAsync(triggerService, pods, cancellationToken))
                    {
                        _log.Warning("'{0}' Trigger service's underlying pods or pod's corresponding port was not found in order to resolve named target port. Ignoring corresponding pod trigger name '{1}' as trigger", new PII(triggerService.Metadata.Name), new PII(pod.Metadata.Name));
                        continue;
                    }

                    var correlationId = string.Empty;
                    try
                    {
                        correlationId = pod.Metadata.GetCorrelationId();
                    }
                    catch (Exception ex)
                    {
                        // This is not critical and, if we fail to get the CorrelationId, we should keep working
                        _log.Warning(ex.Message);
                    }

                    var podTriggerToAdd =
                        new PodTriggerConfig(
                            namespaceName: triggerService.Metadata.NamespaceProperty,
                            triggerService: triggerService,
                            lpkPodName: pod.Metadata.Name,
                            routeOnHeaderKey: routeOnHeader.headerName,
                            routeOnHeaderValue: routeOnHeader.headerValue,
                            triggerPodIP: pod_latest == null ? pod.Status.PodIP : pod_latest.Status.PodIP,
                            correlationId: correlationId);
                    routingStateEstablisherInputMap.AddOrUpdateWithTrigger(triggerService, podTriggerToAdd);
                }
            }
        }

        private async Task AddLoadBalancerTriggersAsync(
            ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> routingStateEstablisherInputMap,
            IEnumerable<V1Service> userServices,
            IEnumerable<V1Pod> pods,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (var service in userServices)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (service.Spec?.Selector == null)
                    {
                        continue;
                    }
                    if (!string.IsNullOrEmpty(service.Spec.Type) && StringComparer.OrdinalIgnoreCase.Equals(service.Spec.Type, KubernetesConstants.TypeStrings.LoadBalancer))
                    {
                        if (!await ReplaceServiceNamedPortsAsync(service, pods, cancellationToken))
                        {
                            _log.Warning("'{0}' Trigger service's underlying pods not found in order to resolve named target port. Ignoring corresponding load balancer trigger", service?.Metadata?.Name ?? string.Empty);
                            continue;
                        }

                        // the below logic checks of the service of type load balancer belongs to an ingress controller
                        var isLoadBalancerIngressController = false;
                        var serviceSelectedPod =
                            pods.FirstOrDefault(pod =>
                                                    pod.Metadata.Labels != null
                                                    && service.Spec.Selector.All(selector =>
                                                                                pod.Metadata.Labels.TryGetValue(selector.Key, out var labelValue)
                                                                                && StringComparer.OrdinalIgnoreCase.Equals(labelValue, selector.Value)));
                        if (serviceSelectedPod?.Spec?.Containers != null)
                        {
                            if (serviceSelectedPod.Spec.Containers.Any(container => !string.IsNullOrEmpty(container.Image) && Constants.IngressControllerImageNames.Any(icImageName => container.Image.Contains(icImageName))))
                            {
                                _log.Info("Service {0} of type load balancer is an ingress controller.", new PII(service.Metadata.Name));
                                isLoadBalancerIngressController = true;
                            }
                            else
                            {
                                _log.Info("Service {0} of type load balancer is not an ingress controller. Image: {1}",
                                    new PII(service.Metadata.Name),
                                    new PII(string.Join(",", serviceSelectedPod.Spec.Containers.Select(c => string.IsNullOrEmpty(c.Image) ? string.Empty : c.Image))));
                            }
                        }

                        var loadBalancerTriggerToAdd = new LoadBalancerTriggerConfig(
                            namespaceName: service.Metadata.NamespaceProperty,
                            triggerService: service,
                            triggerEntityName: service.Metadata.Name,
                            isLoadBalancerIngressController);
                        routingStateEstablisherInputMap.AddOrUpdateWithTrigger(service, loadBalancerTriggerToAdd);
                    }
                }
            }
            catch (NullReferenceException e)
            {
                _log.Error("Null ref in AddLoadBalancerTriggersAsync - message : {0}. Call stack: {1}. Data: {2}", e.Message, e.StackTrace, JsonSerializer.Serialize(e.Data));
                throw;
            }
        }

        /// <summary>
        /// Some services in the user cluster may be using named ports. Replace such values in the service objects
        /// with the actual int values of ports by scanning the respective pods for the service
        /// </summary>
        private Task<bool> ReplaceServiceNamedPortsAsync(V1Service service, IEnumerable<V1Pod> pods, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                V1Pod selectedPod = null;
                foreach (var triggerServicePort in service.Spec.Ports)
                {
                    int port_int = -1;
                    if (!int.TryParse(triggerServicePort.TargetPort.Value, out port_int))
                    {
                        if (selectedPod == null)
                        {
                            // try to find a pod for this service by matching label selector
                            selectedPod =
                                pods.FirstOrDefault(pod =>
                                    service.Spec?.Selector != null
                                    && service.Spec.Selector.All(selector => pod.Metadata.Labels != null
                                                             && pod.Metadata.Labels.ContainsKey(selector.Key)
                                                             && StringComparer.OrdinalIgnoreCase.Equals(pod.Metadata.Labels[selector.Key], selector.Value)));

                            if (selectedPod == null)
                            {
                                return false;
                            }
                        }

                        bool found = false;
                        foreach (var container in selectedPod.Spec.Containers)
                        {
                            // Container ports could be null in case it is a sidecar container which doesn't need to be accessed through a service
                            if (container.Ports == null)
                            {
                                continue;
                            }
                            foreach (var containerPort in container.Ports)
                            {
                                if (StringComparer.OrdinalIgnoreCase.Equals(containerPort.Name, triggerServicePort.TargetPort))
                                {
                                    _log.Info("Identified named port '{0}' in service '{1}'", new PII(containerPort.Name), new PII(service.Metadata.Name));
                                    triggerServicePort.TargetPort = containerPort.ContainerPort;
                                    found = true;
                                    break;
                                }
                            }

                            if (found)
                            {
                                break;
                            }
                        }

                        if (!found)
                        {
                            return false;
                        }
                    }
                }

                return true;
            }, cancellationToken);
        }

        private Task<(string, int)> GetProtocolAndPortNumberFromServiceNamedPort(V1Service service, IntstrIntOrString servicePortFromIngress, string ingressName, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                V1ServicePort servicePort;

                int port_int = -1;
                if (!int.TryParse(servicePortFromIngress.Value, out port_int))
                {
                    servicePort = service.Spec.Ports?.FirstOrDefault(port => StringComparer.OrdinalIgnoreCase.Equals(port.Name, servicePortFromIngress.Value));
                }
                else
                {
                    servicePort = service.Spec.Ports?.FirstOrDefault(port => port.Port == port_int);
                }

                if (servicePort == null)
                {
                    _log.Error("Service port '{0}' from ingress '{1}' does not match any port on the service '{2}'. ", servicePortFromIngress.Value, new PII(ingressName), new PII(service.Metadata.Name));
                    throw new RoutingException(Resources.FailedToMatchServicePortFromIngressFormat, servicePortFromIngress.Value, ingressName, service.Metadata.Name);
                }

                return (servicePort.Protocol, servicePort.Port);
            }, cancellationToken);
        }

        /// <summary>
        /// List ingresses
        /// </summary>
        private async Task<IEnumerable<V1Ingress>> GetIngressesAsync(CancellationToken cancellationToken)
        {
            return (await _kubernetesClient.ListIngressesInNamespaceAsync(_routingManagerConfig.GetNamespace(), cancellationToken: cancellationToken)).Items
                .OrderBy(ing => ing.Metadata.Name);
        }

        /// <summary>
        /// List services
        /// </summary>
        private async Task<IEnumerable<V1Service>> GetServicesAsync(CancellationToken cancellationToken)
        {
            return (await _kubernetesClient.ListServicesInNamespaceAsync(_routingManagerConfig.GetNamespace(), cancellationToken: cancellationToken)).Items
                .OrderBy(svc => svc.Metadata.Name);
        }

        /// <summary>
        /// List deployments
        /// </summary>
        private async Task<IEnumerable<V1Deployment>> GetDeploymentsAsync(CancellationToken cancellationToken)
        {
            return (await _kubernetesClient.ListDeploymentsInNamespaceAsync(_routingManagerConfig.GetNamespace(), cancellationToken: cancellationToken)).Items
                .OrderBy(svc => svc.Metadata.Name);
        }

        /// <summary>
        /// List config maps
        /// </summary>
        private async Task<IEnumerable<V1ConfigMap>> GetConfigMapsAsync(CancellationToken cancellationToken)
        {
            return (await _kubernetesClient.ListNamespacedConfigMapAsync(_routingManagerConfig.GetNamespace(), cancellationToken: cancellationToken)).Items
                .OrderBy(svc => svc.Metadata.Name);
        }

        /// <summary>
        /// List pods
        /// </summary>
        private async Task<IEnumerable<V1Pod>> GetPodsAsync(CancellationToken cancellationToken)
        {
            return (await _kubernetesClient.ListPodsInNamespaceAsync(_routingManagerConfig.GetNamespace(), cancellationToken: cancellationToken)).Items
                .OrderBy(svc => svc.Metadata.Name);
        }

        /// <summary>
        /// List ingressRoutes
        /// </summary>
        private async Task<IEnumerable<IngressRoute>> GetIngressRoutesAsync(CancellationToken cancellationToken)
        {
            return (await _kubernetesClient.ListNamespacedIngressRoutesAsync(_routingManagerConfig.GetNamespace(), cancellationToken: cancellationToken))
                .OrderBy(ingressRoute => ingressRoute.Metadata.Name);
        }
    }
}