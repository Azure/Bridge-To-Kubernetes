// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.RoutingManager.Envoy;
using Microsoft.BridgeToKubernetes.RoutingManager.Logging;
using Microsoft.BridgeToKubernetes.RoutingManager.Traefik;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    /// <summary>
    /// Routing state manager takes an input the pod and ingress triggers and determines the changes to be applied to the cluster
    /// for each of the triggers and applies the same to the cluster. The changes includes new deployments and config maps for envoy
    /// and updates to the user services.
    /// </summary>
    internal class RoutingStateEstablisher
    {
        private ILog _log;
        private IOperationContext _operationContext;
        private IKubernetesClient _kubernetesClient;
        private IEnumerable<V1Service> _inClusterServices;
        private IEnumerable<V1Deployment> _inClusterDeployments;
        private IEnumerable<V1ConfigMap> _inclusterConfigMaps;
        private IEnumerable<V1Ingress> _inClusterIngresses;
        private IEnumerable<IngressRoute> _inClusterIngressRoutes;
        private EnvoyConfigBuilder _envoyConfigBuilder;

        public RoutingStateEstablisher(
            ILog log,
            IOperationContext operationContext,
            IKubernetesClient kubernetesClient,
            IEnumerable<V1Service> inClusterServices,
            IEnumerable<V1Deployment> inClusterDeployments,
            IEnumerable<V1ConfigMap> inClusterConfigMaps,
            IEnumerable<V1Ingress> inClusterIngresses,
            IEnumerable<IngressRoute> inClusterIngressRoutes,
            EnvoyConfigBuilder envoyConfigBuilder)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _inClusterServices = inClusterServices ?? throw new ArgumentNullException(nameof(inClusterServices));
            _inClusterDeployments = inClusterDeployments ?? throw new ArgumentNullException(nameof(inClusterDeployments));
            _inclusterConfigMaps = inClusterConfigMaps ?? throw new ArgumentNullException(nameof(inClusterConfigMaps));
            _inClusterIngresses = inClusterIngresses ?? throw new ArgumentNullException(nameof(inClusterIngresses));
            _inClusterIngressRoutes = inClusterIngressRoutes ?? throw new ArgumentNullException(nameof(inClusterIngressRoutes));
            _envoyConfigBuilder = envoyConfigBuilder ?? throw new ArgumentNullException(nameof(envoyConfigBuilder));
        }

        /// <summary>
        /// Takes as input, a list of <see cref="RoutingStateEstablisherInput"/> and reconciles the cluster to changes required for
        /// input pod and ingress triggers
        /// </summary>
        public async Task<IDictionary<string, string>> RunAsync(IDictionary<V1Service, RoutingStateEstablisherInput> inputs, CancellationToken cancellationToken)
        {
            _log.Info("Establishing routing state...");

            var currentServices = _inClusterServices.Generated(true);
            var currentDeployments = _inClusterDeployments.Generated(true);
            var currentConfigMaps = _inclusterConfigMaps.Generated(true);
            var currentIngresses = _inClusterIngresses.Generated(true);
            var currentIngressRoutes = _inClusterIngressRoutes.Generated(true);

            var expectedServices = new List<V1Service>();
            var expectedDeployments = new List<V1Deployment>();
            var expectedConfigMaps = new List<V1ConfigMap>();
            var expectedIngresses = new List<V1Ingress>();
            var userIngresses = new List<V1Ingress>();
            var expectedIngressRoutes = new List<IngressRoute>();
            var userIngressRoutes = new List<IngressRoute>();

            // Sort allPodTriggers so that the construction of the envoy config maps is deterministic each time
            var allPodTriggersInNamespace = inputs.SelectMany(input => input.Value.PodTriggers).OrderBy(trigger => trigger.TriggerEntityName);

            if (allPodTriggersInNamespace == null || !allPodTriggersInNamespace.Any())
            {
                _operationContext.LoggingProperties[LoggingConstants.Property.PodCorrelationIds] = new Dictionary<string, string>();
                _log.Info("No pod triggers found, skipping envoy and service_clone resource generation");
            }
            else
            {
                _operationContext.LoggingProperties[LoggingConstants.Property.PodCorrelationIds] = allPodTriggersInNamespace.ToDictionary(p => new PII(p.TriggerEntityName).ScrambledValue, p => p.CorrelationId).OrderBy(kp => kp.Key);
                using (var perfLogger = _log.StartPerformanceLogger(Events.RoutingManager.AreaName, Events.RoutingManager.Operations.GenerateResources))
                {
                    var generateResourcesTasks = new List<Task>
                    {
                        Task.Run(() =>
                        {
                            foreach(var input in inputs)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                _log.Verbose("Namespace '{0}' : Trigger service name : '{1}'", new PII(input.Key.Metadata.NamespaceProperty), new PII(input.Key.Metadata.Name));
                                expectedServices.Add(GenerateClonedService(input));
                                expectedDeployments.Add(GenerateEnvoyDeployment(input));
                                expectedConfigMaps.Add(GenerateEnvoyConfigMap(input, _envoyConfigBuilder.GetEnvoyConfig(input.Key, input.Value, allPodTriggersInNamespace)));
                            }
                        },
                        cancellationToken: cancellationToken)
                    };

                    userIngresses = _inClusterIngresses.Generated(false).ToList();
                    generateResourcesTasks.Add(Task.Run(() =>
                    {
                        // Clone ingresses
                        foreach (var podTrigger in allPodTriggersInNamespace)
                        {
                            // If we have already generated ingresses using the same routing header value from another pod trigger(s), just update their Trigger entity label and continue to the next pod trigger.
                            IEnumerable<V1Ingress> matchingIngresses = expectedIngresses.Where(ingress => ingress.Spec.Rules.Any(rule => StringComparer.OrdinalIgnoreCase.Equals(rule.Host.Split(".").First(), podTrigger.RouteOnHeader.Value)));
                            if (matchingIngresses.Any())
                            {
                                _log.Info("While generating ingresses, we found a pod trigger we processed previously with the same route on header value, so updating the existing ingress labels and continuing");
                                matchingIngresses.ExecuteForEach(matchingIngress =>
                                    matchingIngress.Metadata.Labels[Constants.TriggerEntityLabel] = AppendEntityNameToExistingEntityLabelValue(matchingIngress.Metadata.Labels[Constants.TriggerEntityLabel], podTrigger.TriggerEntityName));
                                continue;
                            }

                            foreach (var ingress in userIngresses)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (ingress.Spec.Rules?.Any(rule => string.IsNullOrWhiteSpace(rule.Host)) == true)
                                {
                                    // We do not need to generate a cloned ingress for empty host i.e. "" because it will accept all ingresses, even with a username as a prefix
                                    _log.Verbose("Ignoring ingress '{0}' since it has an empty host.", new PII(ingress.Metadata.Name));
                                    continue;
                                }
                                if (ingress.Spec.Rules?.Any(rule => Constants.WildcardHostRegex.IsMatch(rule.Host)) == true)
                                {
                                    // We also do not want to generate a cloned ingress for wildcard hosts i.e. *.abc.com because we do not support that yet
                                    _log.Verbose("Ignoring ingress '{0}' since it is wildcard.", new PII(ingress.Metadata.Name));
                                    continue;
                                }
                                expectedIngresses.Add(GenerateClonedIngress(ingress, podTrigger));
                            }
                        }
                    },
                    cancellationToken: cancellationToken));

                    userIngressRoutes = _inClusterIngressRoutes.Generated(false).ToList();
                    if (userIngressRoutes != null && userIngressRoutes.Any())
                    {
                        generateResourcesTasks.Add(Task.Run(() =>
                        {
                            // Clone ingress routes
                            foreach (var podTrigger in allPodTriggersInNamespace)
                            {
                                // If we have already generated ingress route using the same routing header value from another pod trigger(s), just update their Trigger entity label and continue to the next pod trigger.
                                IEnumerable<IngressRoute> matchingIngressRoutes = expectedIngressRoutes.Where(ingressRoute => ingressRoute.GetIngressRouteHostForRoute()?.Any(host => StringComparer.OrdinalIgnoreCase.Equals(host.Split(".").First(), podTrigger.RouteOnHeader.Value)) == true);
                                if (matchingIngressRoutes.Any())
                                {
                                    _log.Info("While generating ingressRoutes, we found a pod trigger we processed previously with the same route on header value, so updating the existing ingressRoute labels and continuing");
                                    matchingIngressRoutes.ExecuteForEach(matchingIngressRoute =>
                                        matchingIngressRoute.Metadata.Labels[Constants.TriggerEntityLabel] = AppendEntityNameToExistingEntityLabelValue(matchingIngressRoute.Metadata.Labels[Constants.TriggerEntityLabel], podTrigger.TriggerEntityName));
                                    continue;
                                }

                                foreach (var ingressRoute in userIngressRoutes)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    var ingressRouteHosts = ingressRoute.GetIngressRouteHostForRoute();
                                    // TODO USER STORY 1326960: Check if wildcard * hosts are supported by IngressRoute
                                    if (ingressRouteHosts == null || !ingressRouteHosts.Any())
                                    {
                                        // We do not need to clone the ingressRoute since it already accepts all hosts so it will accept hosts starting with routeAs header also
                                        _log.Verbose("Ignoring ingressRoute '{0}' since it has an empty host.", new PII(ingressRoute.Metadata.Name));
                                        continue;
                                    }
                                    expectedIngressRoutes.Add(GenerateClonedIngressRoute(ingressRoute, podTrigger));
                                }
                            }
                        },
                        cancellationToken: cancellationToken));
                    }

                    await Task.WhenAll(generateResourcesTasks);
                    perfLogger.SetSucceeded();
                }
            }

            if (expectedServices.Count() != expectedDeployments.Count()
                    || expectedServices.Count() != expectedConfigMaps.Count())
            {
                _log.Error("Number of generated envoy resources do not match. ");
                throw new RoutingException(Resources.FailedToValidateEnvoyResources);
            }

            if (allPodTriggersInNamespace != null && allPodTriggersInNamespace.Any())
            {
                var emptyIngressesCount = userIngresses.Count(userIngress => userIngress.Spec.Rules?.Any(rule => string.IsNullOrWhiteSpace(rule.Host)) == true);
                var wildcardIngressesCount = userIngresses.Count(userIngress => userIngress.Spec.Rules?.Any(rule => !string.IsNullOrWhiteSpace(rule.Host) && Constants.WildcardHostRegex.IsMatch(rule.Host)) == true);

                // Count of pod triggers that have unique routing header values
                var allUniquePodTriggersCount =
                    allPodTriggersInNamespace.GroupBy(trigger => trigger.RouteOnHeader.Value)
                                  .Select(g => g.First())
                                  .Count();
                if (expectedIngresses.Count() != ((userIngresses.Count() - wildcardIngressesCount - emptyIngressesCount) * allUniquePodTriggersCount))
                {
                    _log.Error($"Number of expected ingresses is {expectedIngresses.Count()}, but number of (user ingresses * pod triggers) is {userIngresses.Count() * allUniquePodTriggersCount} ");
                    throw new RoutingException(Resources.FailedToValidateGeneratedIngressCountFormat, expectedIngresses.Count(), userIngresses.Count() * allUniquePodTriggersCount);
                }
            }
            else if ((allPodTriggersInNamespace == null || !allPodTriggersInNamespace.Any()) && expectedIngresses.Count() != 0)
            {
                _log.Error("Number of expected ingresses should be 0, but the count is actually '{0}'. ", expectedIngresses.Count());
                throw new RoutingException(Resources.GeneratedIngressCountInvalidFormat, expectedIngresses.Count());
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var perfLogger = _log.StartPerformanceLogger(Events.RoutingManager.AreaName, Events.RoutingManager.Operations.UpdateClusterState))
            {
                // update cluster state
                await Task.WhenAll(
                UpdateClusterStateAsync(currentConfigMaps, expectedConfigMaps, KubernetesResourceType.ConfigMap, cancellationToken),
                UpdateClusterStateAsync(currentServices, expectedServices, KubernetesResourceType.Service, cancellationToken),
                UpdateClusterStateAsync(currentDeployments, expectedDeployments, KubernetesResourceType.Deployment, cancellationToken),
                UpdateClusterStateAsync(currentIngresses, expectedIngresses, KubernetesResourceType.Ingress, cancellationToken),
                UpdateClusterStateAsync(currentIngressRoutes, expectedIngressRoutes, KubernetesResourceType.IngressRoute, cancellationToken));

                var deletePodTasks = new List<Task>();
                // We need to restart those envoy pods whose mounted config maps have been updated
                foreach (var expectedDeployment in expectedDeployments)
                {
                    bool restart = false;
                    // Identify the deployments that got replaced
                    V1Deployment currentDeployment = null;
                    if ((currentDeployment = currentDeployments.FirstOrDefault(d => StringComparer.OrdinalIgnoreCase.Equals(d.Metadata.Name, expectedDeployment.Metadata.Name) && StringComparer.OrdinalIgnoreCase.Equals(d.Metadata.NamespaceProperty, expectedDeployment.Metadata.NamespaceProperty))) != null)
                    {
                        _log.Info("Deployment '{0}' got replaced", currentDeployment.Metadata.Name);
                        V1Volume expectedVolume = null, currentVolume = null;
                        if ((expectedVolume = expectedDeployment.Spec.Template.Spec.Volumes.FirstOrDefault(v => StringComparer.OrdinalIgnoreCase.Equals(v.Name, GetEnvoyConfigMapVolumeName()))) != null)
                        {
                            if ((currentVolume = currentDeployment.Spec.Template.Spec.Volumes.FirstOrDefault(v => StringComparer.OrdinalIgnoreCase.Equals(v.Name, GetEnvoyConfigMapVolumeName()))) != null)
                            {
                                if (!expectedVolume.ConfigMap.Name.Equals(currentVolume.ConfigMap.Name)
                                    || !expectedConfigMaps.First(cm => cm.Metadata.Name.Equals(expectedVolume.ConfigMap.Name)).IsEqual(currentConfigMaps.First(cm => cm.Metadata.Name.Equals(expectedVolume.ConfigMap.Name)), _log))
                                {
                                    _log.Info("Envoy config map for deployment '{0}' was added/updated", new PII(currentDeployment.Metadata.Name));
                                    restart = true;
                                }
                            }
                            else
                            {
                                _log.Info("Envoy config map for deployment '{0}' was added", new PII(currentDeployment.Metadata.Name));
                                restart = true;
                            }
                        }
                    }

                    if (restart)
                    {
                        _log.Info("Restarting pods under the deployment '{0}' in namespace '{1}'", new PII(expectedDeployment.Metadata.Name), new PII(expectedDeployment.Metadata.NamespaceProperty));
                        deletePodTasks.Add(Task.Run(async () =>
                        {
                            var pods = await _kubernetesClient.ListPodsInNamespaceAsync(expectedDeployment.Metadata.NamespaceProperty, expectedDeployment.Spec.Selector.MatchLabels, cancellationToken: cancellationToken);
                            _log.Info("Deleting pods due to updates to envoy config : '{0}'", string.Join(",", pods.Items.Select(pod => pod.Metadata.Name)));
                            await pods.Items.ExecuteForEachAsync(async (pod) =>
                                 await _kubernetesClient.DeleteV1PodAsync(pod.Metadata.NamespaceProperty, pod.Metadata.Name, cancellationToken: cancellationToken));
                        }, cancellationToken));
                    }
                }

                await Task.WhenAll(deletePodTasks);

                perfLogger.SetSucceeded();
            }

            cancellationToken.ThrowIfCancellationRequested();

            var podNamesStatus = new Dictionary<string, string>();
            var ingressErrors = new StringBuilder();
            var ingressRouteErrors = new StringBuilder();
            if (allPodTriggersInNamespace == null || !allPodTriggersInNamespace.Any())
            {
                _log.Info("no pod triggers found, skipping redirect traffic through envoy pod");
            }
            else
            {
                bool[] redirectTrafficThroughEnvoyPodTaskResult;
                using (var perfLogger = _log.StartPerformanceLogger(Events.RoutingManager.AreaName, Events.RoutingManager.Operations.RedirectTrafficThroughEnvoyPod))
                {
                    redirectTrafficThroughEnvoyPodTaskResult = await RedirectTrafficThroughEnvoyPodAsync(inputs, cancellationToken);

                    for (int i = 0; i < inputs.Count(); i++)
                    {
                        var input = inputs.ElementAt(i);
                        if (input.Value.IngressTriggers != null && input.Value.IngressTriggers.Any())
                        {
                            foreach (var ingressTrigger in input.Value.IngressTriggers)
                            {
                                _log.Info("Result of setting up ingress trigger : '{0}' : '{1}'", new PII(ingressTrigger.TriggerEntityName), redirectTrafficThroughEnvoyPodTaskResult[i]);
                                if (!redirectTrafficThroughEnvoyPodTaskResult[i])
                                {
                                    ingressErrors.Append(string.Format("Redirecting user service {0} to its envoy pod for ingress {1} failed. ", ingressTrigger.TriggerService.Metadata.Name, ingressTrigger.TriggerEntityName));
                                }
                            }
                        }
                        if (input.Value.IngressRouteTriggers != null && input.Value.IngressRouteTriggers.Any())
                        {
                            foreach (var ingressRouteTrigger in input.Value.IngressRouteTriggers)
                            {
                                _log.Info("Result of setting up ingressRoute trigger : '{0}' : '{1}'", new PII(ingressRouteTrigger.TriggerEntityName), redirectTrafficThroughEnvoyPodTaskResult[i]);
                                if (!redirectTrafficThroughEnvoyPodTaskResult[i])
                                {
                                    ingressRouteErrors.Append(string.Format("Redirecting user service {0} to its envoy pod for ingressRoute {1} failed. ", ingressRouteTrigger.TriggerService.Metadata.Name, ingressRouteTrigger.TriggerEntityName));
                                }
                            }
                        }
                        if (input.Value.PodTriggers != null && input.Value.PodTriggers.Any())
                        {
                            foreach (var podTrigger in input.Value.PodTriggers)
                            {
                                _log.Info("Result of setting up pod trigger : '{0}' : '{1}'", new PII(podTrigger.TriggerEntityName), redirectTrafficThroughEnvoyPodTaskResult[i]);
                                if (redirectTrafficThroughEnvoyPodTaskResult[i])
                                {
                                    podNamesStatus.Add(podTrigger.TriggerEntityName, string.Empty);
                                }
                                else
                                {
                                    podNamesStatus.Add(podTrigger.TriggerEntityName, string.Format("Redirecting user service {0} to its envoy pod for pod {1} failed. ", podTrigger.TriggerService.Metadata.Name, podTrigger.TriggerEntityName));
                                }
                            }
                        }
                    }
                    perfLogger.SetSucceeded();
                }
            }

            using (var perfLogger = _log.StartPerformanceLogger(Events.RoutingManager.AreaName, Events.RoutingManager.Operations.TreatDanglingServices))
            {
                // If certain previously existing triggers got deleted (ex. an ingress/ingressRoute was deleted/devhostagent pod trigger was deleted),
                // the associated generated envoy service, envoy deployment and service_clone were also deleted in UpdateClusterState above
                // such trigger services are right now not pointing to any pod (dangling)
                // We need to ensure the trigger service then uses the original selectors and points to original user pod
                // we also need to remove the original selector annotation from the trigger service
                var treatDanglingServiceTasks = new List<Task>();
                var existingTriggerServices = _inClusterServices.Where(
                                                svc => svc.Metadata.Annotations != null
                                                && svc.Metadata.Annotations.ContainsKey(Routing.OriginalServiceSelectorAnnotation)
                                                && (svc.Metadata.Labels == null ||
                                                !svc.Metadata.Labels.ContainsKey(Routing.GeneratedLabel)));
                foreach (var existingTriggerService in existingTriggerServices)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var podMetadatasToCheck = new List<V1ObjectMeta>(expectedDeployments.Select(deploy => deploy.Spec.Template.Metadata));
                    if (!podMetadatasToCheck.Any(podMetadatasToCheck => existingTriggerService.Spec.Selector.All(selector => podMetadatasToCheck.Labels.ContainsKey(selector.Key) && StringComparer.OrdinalIgnoreCase.Equals(podMetadatasToCheck.Labels[selector.Key], selector.Value))))
                    {
                        // this trigger service is dangling
                        // update this service's selectors and remove the original selector annotation
                        _log.Info($"Dangling service '{existingTriggerService.Metadata.Name}.{existingTriggerService.Metadata.NamespaceProperty}' found");

                        var originalSelectors = existingTriggerService.Metadata.GetOriginalServiceSelectors();
                        existingTriggerService.Spec.Selector = originalSelectors;
                        existingTriggerService.Metadata.Annotations.Remove(Routing.OriginalServiceSelectorAnnotation);
                        treatDanglingServiceTasks.Add(_kubernetesClient.ReplaceV1ServiceAsync(existingTriggerService.Metadata.NamespaceProperty, existingTriggerService, existingTriggerService.Metadata.Name, cancellationToken: cancellationToken));
                    }
                }
                try
                {
                    await Task.WhenAll(treatDanglingServiceTasks);
                }
                catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.Conflict)
                {
                    _log.Warning("Dangling service wasn't updated: '{0}'. Will attempt to fix this in next run. Ignoring.", new PII(e.Request.RequestUri.ToString()));
                }
                perfLogger.SetSucceeded();
            }

            for (int i = 0; i < podNamesStatus.Count; i++)
            {
                var key = podNamesStatus.Keys.ElementAt(i);
                podNamesStatus[key] += $"{ingressErrors} {ingressRouteErrors}";
            }

            return podNamesStatus;
        }

        /// <summary>
        /// Create deployment object for Envoy
        /// </summary>
        private V1Deployment GenerateEnvoyDeployment(KeyValuePair<V1Service, RoutingStateEstablisherInput> input)
        {
            _log.Verbose("Generating envoy deployment for service '{0}'", input.Key.Metadata.Name);
            var containerPorts = new List<V1ContainerPort>();
            // TODO: This is nasty and needs a better fix
            // As of today we already now that at this point the targetPorts are already all integers because Pragya did so in ReplaceNamedTargetPortsInTriggerServicesAsync
            // This should also be taken care of when we'll migrate to non BETA types:https://devdiv.visualstudio.com/DevDiv/_boards/board/t/Mindaro/Stories/?workitem=1269558
            // With the new non BETA types we don't need intOrString anymore.
            input.Key.Spec.Ports.ExecuteForEach(svcPort => containerPorts.Add(new V1ContainerPort(Int32.Parse(svcPort.TargetPort.Value))));
            var envoyDeploymentName = GetEnvoyDeploymentName(input.Key.Metadata.Name);
            var envoyDeployment = new V1Deployment
            {
                Metadata = new V1ObjectMeta
                {
                    Name = envoyDeploymentName,
                    NamespaceProperty = input.Key.Metadata.NamespaceProperty,
                    Labels = new Dictionary<string, string>
                    {
                        { Routing.GeneratedLabel, "true" },
                        { Constants.EntityLabel, envoyDeploymentName },
                        { Constants.TriggerEntityLabel, GetEntityLabelValue(input.Value) }
                    }
                },
                Spec = new V1DeploymentSpec
                {
                    Strategy = new V1DeploymentStrategy
                    {
                        RollingUpdate = new V1RollingUpdateDeployment
                        {
                            MaxSurge = 2,
                            MaxUnavailable = 1
                        }
                    },
                    Replicas = 2,
                    Selector = new V1LabelSelector
                    {
                        MatchLabels = new Dictionary<string, string>
                        {
                            { Routing.GeneratedLabel, "true" },
                            { Constants.EntityLabel, envoyDeploymentName }
                        }
                    },
                    Template = new V1PodTemplateSpec
                    {
                        Metadata = new V1ObjectMeta
                        {
                            Name = GetEnvoyPodName(input.Key.Metadata.Name),
                            NamespaceProperty = input.Key.Metadata.NamespaceProperty,
                            Labels = new Dictionary<string, string>
                            {
                                { Routing.GeneratedLabel, "true" },
                                { Constants.EntityLabel, envoyDeploymentName }
                            },
                            Annotations = new Dictionary<string, string>
                            {
                                { Annotations.IstioInjection, "false"}
                            }
                        },
                        Spec = new V1PodSpec
                        {
                            TerminationGracePeriodSeconds = 0,
                            Volumes = new List<V1Volume>
                            {
                                {
                                    new V1Volume
                                    {
                                        Name = GetEnvoyConfigMapVolumeName(),
                                        ConfigMap = new V1ConfigMapVolumeSource
                                        {
                                            Name = GetEnvoyConfigMapName(input.Key.Metadata.Name),
                                            Items = new List<V1KeyToPath>
                                            {
                                                {
                                                    new V1KeyToPath
                                                    {
                                                        Key = "config",
                                                        Path = "envoy.yaml"
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            Containers = new List<V1Container>
                            {
                                new V1Container
                                {
                                    Name = "envoy",
                                    Image = Constants.EnvoyImageName,
                                    Command = new List<string> { "/bin/bash" },
                                    Args = new List<string>
                                    {
                                        "-c",
                                        "touch envoy-logs.txt && /usr/local/bin/envoy --log-path envoy-logs.txt --log-level trace --config-path /etc/envoy/envoy.yaml"
                                    },
                                    Ports = containerPorts,
                                    VolumeMounts = new List<V1VolumeMount>
                                    {
                                        {
                                            new V1VolumeMount()
                                            {
                                                Name = "config",
                                                MountPath = "/etc/envoy",
                                                ReadOnlyProperty = true
                                            }
                                        }
                                    }
                                }
                            },
                            NodeSelector = new Dictionary<string, string>() { { KubernetesConstants.Labels.OS, KubernetesConstants.Labels.Values.Linux } }
                        }
                    }
                }
            };

            // If both pod trigger and ingress trigger are not null, then they should mostly contain the same value,
            // but just in case they don't, give more priority to ingress trigger's probes for AGIC case
            if (input.Value.IngressTriggers != null && input.Value.IngressTriggers.Any(i => i.IsAgicIngress))
            {
                // There will only be one container - running the envoy image
                // But there could potentially be multiple pods getting debugged at the same time under this envoy pod
                // i.e. if multiple people are debugging the same service at the same time.
                // Since kubernetes only supports a single liveness or readiness probe to be added to a pod, we will default to the first one
                envoyDeployment.Spec.Template.Spec.Containers.First().ReadinessProbe = input.Value.IngressTriggers.First().HttpReadinessProbe;
                envoyDeployment.Spec.Template.Spec.Containers.First().LivenessProbe = input.Value.IngressTriggers.First().HttpLivenessProbe;
            }

            return envoyDeployment;
        }

        /// <summary>
        /// Generate config map object for envoy
        /// </summary>
        private V1ConfigMap GenerateEnvoyConfigMap(KeyValuePair<V1Service, RoutingStateEstablisherInput> input, EnvoyConfig envoyConfig)
        {
            string envoyConfigYaml;
            var serializer = new YamlDotNet.Serialization.Serializer();
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer, envoyConfig);
                envoyConfigYaml = writer.ToString();
            }

            return new V1ConfigMap
            {
                Metadata = new V1ObjectMeta
                {
                    Name = GetEnvoyConfigMapName(input.Key.Metadata.Name),
                    NamespaceProperty = input.Key.Metadata.NamespaceProperty,
                    Labels = new Dictionary<string, string>
                    {
                        { Routing.GeneratedLabel, "true" },
                        { Constants.TriggerEntityLabel, GetEntityLabelValue(input.Value) }
                    }
                },
                Data = new Dictionary<string, string>
                {
                    { "config",  envoyConfigYaml }
                }
            };
        }

        /// <summary>
        /// Generate the cloned ingress object
        /// </summary>
        private V1Ingress GenerateClonedIngress(V1Ingress ingressToClone, PodTriggerConfig podTrigger)
        {
            _log.Info("Cloning ingress : '{0}' in namespace : '{1}'", new PII(ingressToClone.Metadata.Name), new PII(ingressToClone.Metadata.NamespaceProperty));

            var clonedIngress = ingressToClone.Clone();

            bool isConfiguredWithCertManager = false;
            if (clonedIngress.IsConfiguredWithCertManager())
            {
                isConfiguredWithCertManager = true;
            }

            foreach (var rule in clonedIngress.Spec.Rules)
            {
                if (!string.IsNullOrWhiteSpace(rule.Host))
                {
                    // If "www.contosodev.com" is the original host, then the
                    // variant is something like "bikes-1a2b.www.contosodev.com"
                    rule.Host = $"{podTrigger.RouteOnHeader.Value}.{rule.Host}";
                }
            }

            foreach (var tls in clonedIngress.Spec.Tls)
            {
                if (tls.Hosts != null && tls.Hosts.Any())
                {
                    // TODO: fix the logging of this event to have more info/be less noisy
                    // Bug 1288851: Improvements to curb negative effects of excessive logging by routing manager
                    _log.Event(Events.RoutingManager.Operations.Https);
                    var hosts = new List<string>();
                    foreach (var tlsHost in tls.Hosts)
                    {
                        hosts.Add($"{podTrigger.RouteOnHeader.Value}.{tlsHost}");
                    }
                    tls.Hosts = hosts;
                }

                if (isConfiguredWithCertManager)
                {
                    // If cert manager is enabled, we need to use a different certificate secret
                    // We have already copied over the cert manager annotation, so this certificate secret will be created automatically
                    // In case if cert manager is not enabled, we will be using the same cert secret as the original one
                    tls.SecretName += $"-{podTrigger.RouteOnHeader.Value}-cloned";
                }
            }

            clonedIngress.Metadata.Name = GetClonedIngressName(clonedIngress.Metadata.Name, podTrigger.TriggerEntityName);
            clonedIngress.Metadata.Labels.Add(Routing.GeneratedLabel, "true");
            clonedIngress.Metadata.Labels.Add(Constants.TriggerEntityLabel, GetEntityLabelValueForPodTrigger(podTrigger.TriggerEntityName));

            // If ingress is of type AGIC, we want to prefix the Backendhostname annotation with the pod trigger value
            if (ingressToClone.TryGetAgicBackendHostnameAnnotation(_log, out string agicBackendHostnameAnnotationValue))
            {
                clonedIngress.Metadata.Annotations[Constants.AgicBackendHostnameAnnotation] = $"{podTrigger.RouteOnHeader.Value}.{agicBackendHostnameAnnotationValue}";
            }

            return clonedIngress;
        }

        /// <summary>
        /// Generate the cloned ingressRoute object
        /// </summary>
        private IngressRoute GenerateClonedIngressRoute(IngressRoute ingressRouteToClone, PodTriggerConfig podTrigger)
        {
            _log.Info("Cloning ingressRoute : '{0}' in namespace : '{1}'", new PII(ingressRouteToClone.Metadata.Name), new PII(ingressRouteToClone.Metadata.NamespaceProperty));

            var clonedIngressRoute = ingressRouteToClone.Clone();

            if (clonedIngressRoute.Spec.Virtualhost?.Fqdn != null)
            {
                // If "www.contosodev.com" is the original host, then the
                // variant is something like "bikes-1a2b.www.contosodev.com"
                clonedIngressRoute.Spec.Virtualhost.Fqdn = $"{podTrigger.RouteOnHeader.Value}.{clonedIngressRoute.Spec.Virtualhost.Fqdn}";
            }

            // TODO USER STORY 1326960: Https support: We are assuming that the original manually created cert will be a wildcard
            // to support cloned ingressRoutes as well since IngressRoutes requires you to create a cert manually with Cert manager + letsencrypt
            //if (!string.IsNullOrWhiteSpace(clonedIngressRoute.Spec.Virtualhost?.Tls?.SecretName))
            //{
            //    clonedIngressRoute.Spec.Virtualhost.Tls.SecretName += "-cloned";
            //}

            foreach (var route in clonedIngressRoute.Spec.Routes)
            {
                var routeHost = route.Match.GetHostRegexMatchValue();
                if (!string.IsNullOrEmpty(routeHost))
                {
                    route.Match = route.Match.GetUpdatedMatchHostValue(routeHost, podTrigger.RouteOnHeader.Value);
                }
            }

            clonedIngressRoute.Metadata.Name = GetClonedIngressRouteName(clonedIngressRoute.Metadata.Name, podTrigger.TriggerEntityName);
            clonedIngressRoute.Metadata.Labels.Add(Routing.GeneratedLabel, "true");
            clonedIngressRoute.Metadata.Labels.Add(Constants.TriggerEntityLabel, GetEntityLabelValueForPodTrigger(podTrigger.TriggerEntityName));

            return clonedIngressRoute;
        }

        /// <summary>
        /// Generate the cloned servie object
        /// </summary>
        /// <param name="input"></param>
        private V1Service GenerateClonedService(KeyValuePair<V1Service, RoutingStateEstablisherInput> input)
        {
            var clonedService = input.Key.Clone();

            clonedService.Metadata.Name = KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix(input.Key.Metadata.Name, $"{Constants.ClonedSuffix}-svc");

            clonedService.Metadata.Labels.Add(Routing.GeneratedLabel, "true");
            clonedService.Metadata.Labels.Add(Constants.TriggerEntityLabel, GetEntityLabelValue(input.Value));

            // If this is not the first run of routing manager for this trigger service, the trigger service's selectors would have been modified.
            // Copy the original selectors from our annotation
            if (clonedService.Metadata.Annotations != null && clonedService.Metadata.Annotations.ContainsKey(Routing.OriginalServiceSelectorAnnotation))
            {
                clonedService.Spec.Selector = JsonSerializer.Deserialize<Dictionary<string, string>>(clonedService.Metadata.Annotations[Routing.OriginalServiceSelectorAnnotation]);
            }

            return clonedService;
        }

        /// <summary>
        /// Update the cluster with the expected objects
        /// </summary>
        private Task UpdateClusterStateAsync(
            IEnumerable<IMetadata<V1ObjectMeta>> currentObjectsList,
            IEnumerable<IMetadata<V1ObjectMeta>> expectedObjectsList,
            KubernetesResourceType k8sResourceType,
            CancellationToken cancellationToken)
        {
            // Incrementally create, update and delete services
            var currentObjects = currentObjectsList.ToList().Ordered();
            var expectedObjects = expectedObjectsList.ToList().Ordered();

            int currentIndex = 0, expectedIndex = 0;
            IList<Task> tasks = new List<Task>();
            try
            {
                while (currentIndex < currentObjects.Count() || expectedIndex < expectedObjects.Count())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentMeta = currentIndex < currentObjects.Count() ? currentObjects[currentIndex].Metadata : null;
                    var expectedMeta = expectedIndex < expectedObjects.Count() ? expectedObjects[expectedIndex].Metadata : null;
                    var currentKey = currentMeta != null ? $"{currentMeta.Name}" : null;
                    var pendingKey = expectedMeta != null ? $"{expectedMeta.Name}" : null;
                    var comparison = String.Compare(currentKey, pendingKey);
                    if (currentKey == null || pendingKey == null)
                    {
                        // If there are no more pending ingresses, then the string
                        // comparison will always return >0, but we want it to
                        // return <0 so that it deletes remaining current ingresses.
                        // Same reasoning when there are no more current ingresses.
                        comparison = -comparison;
                    }
                    if (comparison < 0)
                    {
                        switch (k8sResourceType)
                        {
                            case KubernetesResourceType.Service:
                                _log.Info("Deleting service '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.DeleteV1ServiceAsync(currentMeta.NamespaceProperty, currentMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.Deployment:
                                _log.Info("Deleting deployment '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.DeleteDeploymentsInNamespaceAsync(currentMeta.NamespaceProperty, currentMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.ConfigMap:
                                _log.Info("Deleting config map '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.DeleteNamespacedConfigMapAsync(currentMeta.NamespaceProperty, currentMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.Ingress:
                                _log.Info("Deleting ingress '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.DeleteNamespacedIngressAsync(currentMeta.NamespaceProperty, currentMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.IngressRoute:
                                _log.Info("Deleting ingressRoute '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.DeleteNamespacedIngressRouteAsync(currentMeta.NamespaceProperty, currentMeta.Name, cancellationToken: cancellationToken));
                                break;

                            default:
                                _log.Error("Unknown kubernetes object of type '{0}'. ", currentObjects[currentIndex].GetType().Name);
                                throw new RoutingException(Resources.UnknownKubernetesObjectFormat, currentObjects[currentIndex].GetType().Name);
                        }
                        currentIndex++;
                    }
                    else if (comparison == 0)
                    {
                        switch (k8sResourceType)
                        {
                            case KubernetesResourceType.Service:
                                _log.Info("Replacing service '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                var expectedService = expectedObjects[expectedIndex] as V1Service;
                                var currentService = currentObjects[currentIndex] as V1Service;
                                expectedService.Metadata.ResourceVersion = currentService.Metadata.ResourceVersion;
                                expectedService.Spec.ClusterIP = currentService.Spec.ClusterIP;
                                expectedService.Spec.LoadBalancerIP = currentService.Spec.LoadBalancerIP;
                                tasks.Add(_kubernetesClient.ReplaceV1ServiceAsync(expectedMeta.NamespaceProperty, (V1Service)expectedObjects[expectedIndex], expectedMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.Deployment:
                                _log.Info("Replacing deployment '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                var expectedDeployment = (V1Deployment)expectedObjects[expectedIndex];

                                if (expectedMeta.Labels.IsEqual(currentMeta.Labels))
                                {
                                    tasks.Add(_kubernetesClient.ReplaceNamespacedDeploymentAsync(expectedMeta.NamespaceProperty, expectedDeployment, expectedMeta.Name, cancellationToken: cancellationToken));
                                }
                                else
                                {
                                    // Deleting and recreating because in k8s API version apps/v1 (what we use for envoy deployment), label selectors are immutable after creation
                                    tasks.Add(
                                        Task.Run(async () =>
                                        {
                                            await _kubernetesClient.DeleteDeploymentsInNamespaceAsync(expectedMeta.NamespaceProperty, expectedMeta.Name, cancellationToken: cancellationToken);
                                            await _kubernetesClient.CreateNamespacedDeploymentAsync(expectedMeta.NamespaceProperty, expectedDeployment, cancellationToken: cancellationToken);
                                        }));
                                }
                                break;

                            case KubernetesResourceType.ConfigMap:
                                _log.Info("Replacing config map '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                (expectedObjects[expectedIndex] as V1ConfigMap).Metadata.ResourceVersion = (currentObjects[currentIndex] as V1ConfigMap).Metadata.ResourceVersion;
                                tasks.Add(_kubernetesClient.ReplaceNamespacedConfigMapAsync(expectedMeta.NamespaceProperty, (V1ConfigMap)expectedObjects[expectedIndex], expectedMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.Ingress:
                                _log.Info("Replacing ingress '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                (expectedObjects[expectedIndex] as V1Ingress).Metadata.ResourceVersion = (currentObjects[currentIndex] as V1Ingress).Metadata.ResourceVersion;
                                tasks.Add(_kubernetesClient.ReplaceNamespacedIngress1Async(expectedMeta.NamespaceProperty, (V1Ingress)expectedObjects[expectedIndex], expectedMeta.Name, cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.IngressRoute:
                                _log.Info("Replacing ingressRoute '{0}.{1}'", new PII(currentMeta.Name), new PII(currentMeta.NamespaceProperty));
                                (expectedObjects[expectedIndex] as IngressRoute).Metadata.ResourceVersion = (currentObjects[currentIndex] as IngressRoute).Metadata.ResourceVersion;
                                tasks.Add(_kubernetesClient.ApplyNamespacedIngressRouteAsync(expectedMeta.NamespaceProperty, (IngressRoute)expectedObjects[expectedIndex], cancellationToken));
                                break;

                            default:
                                _log.Error("Unknown kubernetes object of type '{0}'. ", currentObjects[currentIndex].GetType().Name);
                                throw new RoutingException(Resources.UnknownKubernetesObjectFormat, currentObjects[currentIndex].GetType().Name);
                        }
                        currentIndex++;
                        expectedIndex++;
                    }
                    else
                    {
                        switch (k8sResourceType)
                        {
                            case KubernetesResourceType.Service:
                                _log.Info("Creating service '{0}.{1}'", new PII(expectedMeta.Name), new PII(expectedMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.CreateNamespacedServiceAsync(expectedMeta.NamespaceProperty, (V1Service)expectedObjects[expectedIndex], cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.Deployment:
                                _log.Info("Creating deployment '{0}.{1}'", new PII(expectedMeta.Name), new PII(expectedMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.CreateNamespacedDeploymentAsync(expectedMeta.NamespaceProperty, (V1Deployment)expectedObjects[expectedIndex], cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.ConfigMap:
                                _log.Info("Creating config map '{0}.{1}'", new PII(expectedMeta.Name), new PII(expectedMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.CreateNamespacedConfigMapAsync(expectedMeta.NamespaceProperty, (V1ConfigMap)expectedObjects[expectedIndex], cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.Ingress:
                                _log.Info("Creating ingress '{0}.{1}'", new PII(expectedMeta.Name), new PII(expectedMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.CreateNamespacedIngressAsync(expectedMeta.NamespaceProperty, (V1Ingress)expectedObjects[expectedIndex], cancellationToken: cancellationToken));
                                break;

                            case KubernetesResourceType.IngressRoute:
                                _log.Info("Creating ingressRoute '{0}.{1}'", new PII(expectedMeta.Name), new PII(expectedMeta.NamespaceProperty));
                                tasks.Add(_kubernetesClient.ApplyNamespacedIngressRouteAsync(expectedMeta.NamespaceProperty, (IngressRoute)expectedObjects[expectedIndex], cancellationToken: cancellationToken));
                                break;
                        }
                        expectedIndex++;
                    }
                }
            }
            catch (HttpOperationException e) when (StringComparer.OrdinalIgnoreCase.Equals(e.Response.ReasonPhrase, Constants.KubernetesError.Conflict))
            {
                _log.Warning("Logged exception from KubernetesClient with reason phrase '{0}' : Response Content : '{1}'", e.Response.ReasonPhrase, e.Response.Content);
                throw;
            }
            catch (HttpOperationException e) when (StringComparer.OrdinalIgnoreCase.Equals(e.Response.ReasonPhrase, Constants.KubernetesError.UnprocessableEntity))
            {
                // bug#1220078 : For processing unprocessableEntity errors arising from here.
                _log.Error("Logged exception from KubernetesClient with reason phrase '{0}' : Response Content : '{1}'", e.Response.ReasonPhrase, e.Response.Content);
                throw;
            }
            // bug 1006600
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Update the user's original service to point to envoy pod
        /// </summary>
        private Task<bool[]> RedirectTrafficThroughEnvoyPodAsync(
            IDictionary<V1Service, RoutingStateEstablisherInput> inputs,
            CancellationToken cancellationToken)
        {
            return inputs.ExecuteForEachAsync(async input =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                V1Deployment envoyDeploymentStatus = null;
                await WebUtilities.RetryUntilTimeWithWaitAsync(async _ =>
                {
                    envoyDeploymentStatus = await _kubernetesClient.ReadNamespacedDeploymentStatusAsync(input.Key.Metadata.NamespaceProperty, GetEnvoyDeploymentName(input.Key.Metadata.Name), cancellationToken: cancellationToken);
                    if (envoyDeploymentStatus.Status.ReadyReplicas == envoyDeploymentStatus.Status.Replicas)
                    {
                        _log.Info("Found ready replicas for envoy deployment for trigger service '{0}'", new PII(input.Key.Metadata.Name));
                        return true;
                    }
                    _log.Warning("Envoy deployment for the trigger service '{0}' does not have any ready replicas", new PII(input.Key.Metadata.Name));
                    return false;
                },
                maxWaitTime: TimeSpan.FromSeconds(15),
                waitInterval: TimeSpan.FromSeconds(1),
                cancellationToken: cancellationToken);

                if (envoyDeploymentStatus.Status.ReadyReplicas == envoyDeploymentStatus.Status.Replicas)
                {
                    if (input.Key.Metadata.Annotations == null)
                    {
                        input.Key.Metadata.Annotations = new Dictionary<string, string>();
                    }

                    // Add the trigger service's selectors to an annotation (if not exists already) so that we can refer to it in subsequent runs
                    if (!input.Key.Metadata.Annotations.ContainsKey(Routing.OriginalServiceSelectorAnnotation))
                    {
                        input.Key.Metadata.Annotations.Add(Routing.OriginalServiceSelectorAnnotation, JsonSerializer.Serialize(input.Key.Spec.Selector));
                    }

                    input.Key.Spec.Selector =
                        new Dictionary<string, string>
                        {
                            { Constants.EntityLabel, GetEnvoyDeploymentName(input.Key.Metadata.Name) },
                            { Routing.GeneratedLabel, "true" }
                        };

                    try
                    {
                        await _kubernetesClient.ReplaceV1ServiceAsync(input.Key.Metadata.NamespaceProperty, input.Key, input.Key.Metadata.Name, cancellationToken: cancellationToken);
                    }
                    catch (HttpOperationException e) when (StringComparer.OrdinalIgnoreCase.Equals(e.Response.ReasonPhrase, Constants.KubernetesError.Conflict))
                    {
                        _log.Warning("Logged exception from KubernetesClient when replacing trigger service to point to envoy pod with reason phrase '{0}' : Response Content : '{1}'", e.Response.ReasonPhrase, e.Response.Content);
                        return false;
                    }
                    catch (HttpOperationException e)
                    {
                        // bug#1220078 : For processing unprocessableEntity errors arising from here.
                        _log.Error("Logged exception from KubernetesClient when replacing trigger service to point to envoy pod with reason phrase '{0}' : Response Content : '{1}'", e.Response.ReasonPhrase, e.Response.Content);
                        throw;
                    }
                    _log.Info("Updated trigger service '{0}' to point to envoy pods", new PII(input.Key.Metadata.Name));
                    return true;
                }

                return false;
            });
        }

        private string GetClonedIngressName(string ingressName, string triggerPodName)
            => KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix($"{ingressName}-{triggerPodName}", $"{Constants.ClonedSuffix}");

        private string GetClonedIngressRouteName(string ingressRouteName, string triggerPodName)
            => KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix($"{ingressRouteName}-{triggerPodName}", $"{Constants.ClonedSuffix}");

        private string GetEnvoyDeploymentName(string triggerServiceName)
            => KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix($"{triggerServiceName}", $"{Constants.EnvoySuffix}-deploy");

        private string GetEnvoyPodName(string triggerServiceName)
            => KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix($"{triggerServiceName}", $"{Constants.EnvoySuffix}-pod");

        private string GetEnvoyConfigMapName(string triggerServiceName)
            => KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix($"{triggerServiceName}", $"{Constants.EnvoySuffix}-cm");

        private string GetEnvoyConfigMapVolumeName()
            => "config";

        private string GetEntityLabelValue(RoutingStateEstablisherInput input)
        {
            const string podSuffix = ".p";
            const string ingressSuffix = ".i";
            const string loadBalancerSuffix = ".l";
            int entityCount = input.PodTriggers.Count() + input.IngressTriggers.Count() + (input.LoadBalancerTrigger == null ? 0 : 1);

            int maxLengthByCount = KubernetesConstants.Limits.MaxLabelValueLength / entityCount;
            int maxLengthForEachEntity = maxLengthByCount > entityCount ? maxLengthByCount - entityCount : maxLengthByCount;

            var value = string.Empty;

            if (input.PodTriggers != null && input.PodTriggers.Any())
            {
                value = $"{string.Join("_", input.PodTriggers.Select(it => KubernetesUtilities.GetKubernetesResourceName(it.TriggerEntityName, podSuffix, maxLengthForEachEntity)))}_";
            }

            if (input.IngressTriggers != null && input.IngressTriggers.Any())
            {
                value += $"{string.Join("_", input.IngressTriggers.Select(it => KubernetesUtilities.GetKubernetesResourceName(it.TriggerEntityName, ingressSuffix, maxLengthForEachEntity)))}_";
            }

            if (input.LoadBalancerTrigger != null)
            {
                value += KubernetesUtilities.GetKubernetesResourceName(input.LoadBalancerTrigger.TriggerEntityName, loadBalancerSuffix, maxLengthForEachEntity);
            }

            // Remove any extra trailing '_', '-' or '.' from the splitting and concatenating logic above.
            value = value.Trim(new char[] { '_', '-', '.' });

            // Snip the string again in case we exceeded max length
            value = KubernetesUtilities.GetKubernetesResourceName(value);

            // Use the Kubernetes regex match to ensure it is a valid value
            KubernetesUtilities.IsValidLabelValue(value, _log);

            return value;
        }

        private string AppendEntityNameToExistingEntityLabelValue(string existingEntityLabelValue, string entityNameToAppend)
        {
            return KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix(existingEntityLabelValue, $"_{GetEntityLabelValueForPodTrigger(entityNameToAppend)}");
        }

        /// <summary>
        /// Gets a label value for pod trigger. Ensures that the label value follows kubernetes name restrictions.
        /// </summary>
        /// <param name="podTriggerEntityName"></param>
        /// <returns></returns>
        private string GetEntityLabelValueForPodTrigger(string podTriggerEntityName)
        {
            return KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix(podTriggerEntityName, ".p");
        }
    }
}