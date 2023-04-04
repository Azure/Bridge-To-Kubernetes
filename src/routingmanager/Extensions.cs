// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using k8s;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.RoutingManager.Traefik;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;
using System.Text.Json.Serialization;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using System.Text.Json;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    internal static class Extensions
    {
        public static bool IsGenerated(this V1ObjectMeta metadata)
        {
            return metadata.Labels != null &&
                metadata.Labels.ContainsKey(Routing.GeneratedLabel);
        }

        public static bool IsRoutingTrigger(this V1ObjectMeta metadata)
        {
            return metadata != null
                && metadata.Labels != null
                && metadata.Labels.ContainsKey(Routing.RouteFromLabelName)
                && metadata.Annotations != null
                && metadata.Annotations.ContainsKey(Routing.RouteOnHeaderAnnotationName);
        }

        public static IEnumerable<V1Ingress> Generated(this IEnumerable<V1Ingress> ingresses, bool value)
        {
            return ingresses.Where(ing => ing.Metadata.IsGenerated() ? value : !value);
        }

        public static IEnumerable<V1Service> Generated(this IEnumerable<V1Service> services, bool value)
        {
            return services.Where(svc => svc.Metadata.IsGenerated() ? value : !value);
        }

        public static IEnumerable<V1Deployment> Generated(this IEnumerable<V1Deployment> deployments, bool value)
        {
            return deployments.Where(svc => svc.Metadata.IsGenerated() ? value : !value);
        }

        public static IEnumerable<V1ConfigMap> Generated(this IEnumerable<V1ConfigMap> configMaps, bool value)
        {
            return configMaps.Where(svc => svc.Metadata.IsGenerated() ? value : !value);
        }

        public static IEnumerable<IngressRoute> Generated(this IEnumerable<IngressRoute> ingressRoutes, bool value)
        {
            return ingressRoutes.Where(ingressRoute => ingressRoute.Metadata.IsGenerated() ? value : !value);
        }

        public static string GetRouteFromServiceName(this V1ObjectMeta metadata, ILog log)
        {
            if (metadata != null
                && metadata.Labels != null
                && metadata.Labels.ContainsKey(Routing.RouteFromLabelName))
            {
                return metadata.Labels[Routing.RouteFromLabelName];
            }

            log.Error("Failed to read label value '{0}' from object '{1}'. ", Routing.RouteFromLabelName, new PII(metadata.Name));
            throw new RoutingException(Resources.FailedToReadLabelFormat, Routing.RouteFromLabelName, metadata.Name);
        }

        public static (string headerName, string headerValue) GetRouteOnHeader(this V1ObjectMeta metadata, ILog log)
        {
            var annotationValue = metadata.GetRequiredAnnotationValue(Routing.RouteOnHeaderAnnotationName, log);

            var sections = annotationValue.Split('=', StringSplitOptions.RemoveEmptyEntries);
            if (sections.Length == 2)
            {
                return (sections[0], sections[1]);
            }
            log.Error("Failed to read annotation value '{0}' from object '{1}'. ", Routing.RouteOnHeaderAnnotationName, new PII(metadata.Name));
            throw new RoutingException(Resources.FailedToReadAnnotationFormat, Routing.RouteOnHeaderAnnotationName, metadata.Name);
        }

        public static IDictionary<string, string> GetOriginalServiceSelectors(this V1ObjectMeta metadata)
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(metadata.Annotations[Routing.OriginalServiceSelectorAnnotation]);
        }

        public static string GetCorrelationId(this V1ObjectMeta metadata)
        {
            return metadata.GetOptionalAnnotationValue(Annotations.CorrelationId);
        }

        public static IList<IMetadata<V1ObjectMeta>> Ordered(this IList<IMetadata<V1ObjectMeta>> kubernetesObjects)
        {
            return kubernetesObjects
                   .OrderBy(obj => obj.Metadata.NamespaceProperty)
                   .ThenBy(obj => obj.Metadata.Name)
                   .ToList();
        }

        public static V1Service Clone(this V1Service service)
        {
            var clonedService = new V1Service
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>(),
                    DeletionGracePeriodSeconds = service.Metadata.DeletionGracePeriodSeconds,
                    Finalizers = service.Metadata.Finalizers,
                    Labels = new Dictionary<string, string>(),
                    Name = service.Metadata.Name,
                    NamespaceProperty = service.Metadata.NamespaceProperty
                },
                Spec = new V1ServiceSpec
                {
                    // We do not set ExternalTrafficPolicy, HealthCheckNodePort, LoadBalancerIP and LoadBalancerSourceRanges since these should not be required
                    ExternalName = service.Spec.ExternalName,
                    Ports = new List<V1ServicePort>(),
                    PublishNotReadyAddresses = service.Spec.PublishNotReadyAddresses,
                    Selector = new Dictionary<string, string>(),
                    SessionAffinity = service.Spec.SessionAffinity,
                    SessionAffinityConfig = service.Spec.SessionAffinityConfig,
                    Type = "ClusterIP",
                }
            };

            if (service.Metadata.Annotations != null)
            {
                service.Metadata.Annotations.ExecuteForEach(kv => clonedService.Metadata.Annotations.Add(kv.Key, kv.Value));
            }
            // Add the clonedFrom annotation
            clonedService.Metadata.Annotations.Add(Constants.ClonedFromAnnotation, service.Metadata.Name);

            if (service.Metadata.Labels != null)
            {
                service.Metadata.Labels.ExecuteForEach(kv => clonedService.Metadata.Labels.Add(kv.Key, kv.Value));
            }

            if (service.Spec.Ports != null)
            {
                foreach (var port in service.Spec.Ports)
                {
                    clonedService.Spec.Ports.Add(new V1ServicePort
                    {
                        AppProtocol = port.AppProtocol,
                        Name = port.Name,
                        Port = port.Port,
                        Protocol = port.Protocol,
                        TargetPort = port.TargetPort
                    });
                }
            }

            if (service.Spec.Selector != null)
            {
                foreach (var selector in service.Spec.Selector)
                {
                    clonedService.Spec.Selector.Add(selector.Key, selector.Value);
                }
            }

            return clonedService;
        }

        public static V1Ingress Clone(this V1Ingress ingress)
        {
            var clonedIngress = new V1Ingress
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>(),
                    DeletionGracePeriodSeconds = ingress.Metadata.DeletionGracePeriodSeconds,
                    Finalizers = ingress.Metadata.Finalizers,
                    Labels = new Dictionary<string, string>(),
                    Name = ingress.Metadata.Name,
                    NamespaceProperty = ingress.Metadata.NamespaceProperty,
                },
                Spec = new V1IngressSpec()
                {
                    IngressClassName = ingress.Spec?.IngressClassName,
                    Rules = new List<V1IngressRule>(),
                    Tls = new List<V1IngressTLS>()
                }
            };

            if (ingress.Spec.DefaultBackend != null)
            {
                clonedIngress.Spec.DefaultBackend = new V1IngressBackend
                {
                    Service = ingress.Spec?.DefaultBackend?.Service
                };
            }

            if (ingress.Metadata.Annotations != null)
            {
                ingress.Metadata.Annotations.ExecuteForEach(kv => clonedIngress.Metadata.Annotations.Add(kv.Key, kv.Value));
            }
            // Add the clonedFrom annotation
            clonedIngress.Metadata.Annotations.Add(Constants.ClonedFromAnnotation, ingress.Metadata.Name);

            if (ingress.Metadata.Labels != null)
            {
                ingress.Metadata.Labels.ExecuteForEach(kv => clonedIngress.Metadata.Labels.Add(kv.Key, kv.Value));
            }

            if (ingress.Spec.Rules != null)
            {
                foreach (var rule in ingress.Spec.Rules)
                {
                    var clonedRule = new V1IngressRule
                    {
                        Host = rule.Host,
                        Http = new V1HTTPIngressRuleValue
                        {
                            Paths = new List<V1HTTPIngressPath>()
                        }
                    };

                    foreach (var path in rule.Http.Paths)
                    {
                        var clonedPath = new V1HTTPIngressPath();
                        if (path.Backend != null)
                        {
                            clonedPath.Backend = new V1IngressBackend();
                            if (path.Backend.Resource != null)
                            {
                                clonedPath.Backend.Resource = new V1TypedLocalObjectReference
                                {
                                    ApiGroup = path.Backend.Resource.ApiGroup,
                                    Kind = path.Backend.Resource.Kind,
                                    Name = path.Backend.Resource.Name
                                };
                            }
                            clonedPath.Backend.Service = path.Backend.Service;
                        }

                        clonedPath.Path = path.Path;
                        clonedPath.PathType = path.PathType;
                        clonedRule.Http.Paths.Add(clonedPath);
                    }

                    clonedIngress.Spec.Rules.Add(clonedRule);
                }
            }

            if (ingress.Spec.Tls != null)
            {
                foreach (var tls in ingress.Spec.Tls)
                {
                    clonedIngress.Spec.Tls.Add(
                        new V1IngressTLS
                        {
                            Hosts = new List<string>(tls.Hosts),
                            SecretName = tls.SecretName
                        });
                }
            }

            return clonedIngress;
        }

        // TODO USER STORY 1326960: Add https support
        public static IngressRoute Clone(this IngressRoute ingressRoute)
        {
            var clonedIngressRoute = new IngressRoute
            {
                ApiVersion = ingressRoute.ApiVersion,
                Kind = ingressRoute.Kind,
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>(),
                    DeletionGracePeriodSeconds = ingressRoute.Metadata.DeletionGracePeriodSeconds,
                    Finalizers = ingressRoute.Metadata.Finalizers,
                    Labels = new Dictionary<string, string>(),
                    Name = ingressRoute.Metadata.Name,
                    NamespaceProperty = ingressRoute.Metadata.NamespaceProperty,
                },
                Spec = new Spec
                {
                    Virtualhost = new Virtualhost
                    {
                        Tls = new Tls()
                    },
                    Routes = new List<Route>(),
                    Strategy = ingressRoute.Spec.Strategy,
                    HealthCheckOptional = ingressRoute.Spec.HealthCheckOptional
                }
            };

            if (ingressRoute.Spec.Virtualhost?.Fqdn != null)
            {
                clonedIngressRoute.Spec.Virtualhost.Fqdn = ingressRoute.Spec.Virtualhost.Fqdn;
            }

            if (ingressRoute.Spec.Virtualhost?.Tls != null)
            {
                clonedIngressRoute.Spec.Virtualhost.Tls.MinimumProtocolVersion = ingressRoute.Spec.Virtualhost.Tls.MinimumProtocolVersion;
                clonedIngressRoute.Spec.Virtualhost.Tls.SecretName = ingressRoute.Spec.Virtualhost.Tls.SecretName;
            }

            if (ingressRoute.Metadata.Annotations != null)
            {
                ingressRoute.Metadata.Annotations.ExecuteForEach(kv => clonedIngressRoute.Metadata.Annotations.Add(kv.Key, kv.Value));
            }
            // Add the clonedFrom annotation
            clonedIngressRoute.Metadata.Annotations.Add(Constants.ClonedFromAnnotation, ingressRoute.Metadata.Name);

            if (ingressRoute.Metadata.Labels != null)
            {
                ingressRoute.Metadata.Labels.ExecuteForEach(kv => clonedIngressRoute.Metadata.Labels.Add(kv.Key, kv.Value));
            }

            if (ingressRoute.Spec.Routes != null)
            {
                foreach (var route in ingressRoute.Spec.Routes)
                {
                    var clonedRoute = new Route
                    {
                        Services = new List<Service>(),
                        Delegate = route.Delegate,
                        Match = route.Match,
                        PermitInsecure = route.PermitInsecure
                    };

                    if (route.Services != null)
                    {
                        foreach (var service in route.Services)
                        {
                            clonedRoute.Services.Add(new Service
                            {
                                Name = service.Name,
                                Port = service.Port,
                                Strategy = service.Strategy,
                                Weight = service.Weight,
                                HealthCheckOptional = service.HealthCheckOptional
                            });
                        }
                    }

                    clonedIngressRoute.Spec.Routes.Add(clonedRoute);
                }
            }

            return clonedIngressRoute;
        }

        public static bool IsEqual(this V1ConfigMap configMap, V1ConfigMap configMap1, ILog log)
        {
            if (configMap1 == null)
            {
                log.Verbose("Configmap being compared to is null");
                return false;
            }

            if (((configMap.Metadata.Labels == null || !configMap.Metadata.Labels.Any()) && (configMap1.Metadata.Labels != null || configMap1.Metadata.Labels.Any()))
                || ((configMap.Metadata.Labels != null || configMap.Metadata.Labels.Any()) && (configMap1.Metadata.Labels == null || !configMap1.Metadata.Labels.Any()))
                || configMap.Metadata.Labels.Count != configMap1.Metadata.Labels.Count)
            {
                log.Verbose("Config map labels do not match");
                return false;
            }
            foreach (var label in configMap.Metadata.Labels)
            {
                if (!configMap1.Metadata.Labels.ContainsKey(label.Key)
                    || !configMap1.Metadata.Labels[label.Key].Equals(label.Value))
                {
                    log.Verbose("Config map label values do not match");
                    return false;
                }
            }

            if (((configMap.Data == null || !configMap.Data.Any()) && (configMap1.Data != null || configMap1.Data.Any()))
                || ((configMap.Data != null || configMap.Data.Any()) && (configMap1.Data == null || !configMap1.Data.Any()))
                || configMap.Data.Count != configMap1.Data.Count)
            {
                log.Verbose("Config map data count does not match");
                return false;
            }

            foreach (var data in configMap.Data)
            {
                if (!configMap1.Data.ContainsKey(data.Key)
                    || !configMap1.Data[data.Key].Equals(data.Value))
                {
                    log.Verbose("Config map data values do not match");
                    return false;
                }
            }

            log.Verbose("Config maps match");
            return true;
        }

        public static void AddOrUpdateWithTrigger(this ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> dict, V1Service key, PodTriggerConfig triggerConfig)
        {
            if (!dict.TryAdd(
                key: key,
                value: new RoutingStateEstablisherInput(
                    podTriggers: new List<PodTriggerConfig> { triggerConfig })))
            {
                dict[key].PodTriggers.Add(triggerConfig);
            }
        }

        public static void AddOrUpdateWithTrigger(this ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> dict, V1Service key, IngressTriggerConfig triggerConfig)
        {
            if (!dict.TryAdd(
                key: key,
                value: new RoutingStateEstablisherInput(
                    ingressTriggers: new List<IngressTriggerConfig> { triggerConfig })))
            {
                dict[key].IngressTriggers.Add(triggerConfig);
            }
        }

        public static void AddOrUpdateWithTrigger(this ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> dict, V1Service key, LoadBalancerTriggerConfig triggerConfig)
        {
            if (!dict.TryAdd(
                key: key,
                value: new RoutingStateEstablisherInput(
                    loadBalancerTrigger: triggerConfig)))
            {
                dict[key].AddLoadBalancerTrigger(triggerConfig);
            }
        }

        public static void AddOrUpdateWithTrigger(this ConcurrentDictionary<V1Service, RoutingStateEstablisherInput> dict, V1Service key, IngressRouteTriggerConfig triggerConfig)
        {
            if (!dict.TryAdd(
                key: key,
                value: new RoutingStateEstablisherInput(
                    ingressRouteTriggers: new List<IngressRouteTriggerConfig> { triggerConfig })))
            {
                dict[key].IngressRouteTriggers.Add(triggerConfig);
            }
        }

        public static bool IsEqual(this IDictionary<string, string> labels, IDictionary<string, string> labels2)
        {
            return labels2 != null
                && labels.Count == labels2.Count
                && labels.All(labelKvp => labels2.ContainsKey(labelKvp.Key) && StringComparer.OrdinalIgnoreCase.Equals(labelKvp.Value, labels2[labelKvp.Key]));
        }

        public static bool IsConfiguredWithCertManager(this V1Ingress ingress)
        {
            return ingress.Metadata.Annotations != null
                && ingress.Metadata.Annotations.ContainsKey(Constants.CertManagerAnnotationName);
        }

        public static bool IsAgicIngress(this V1Ingress ingress)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(Constants.IngressClassAnnotationAgicValue, ingress.Metadata.GetOptionalAnnotationValue(Constants.IngressClassAnnotation));
        }

        public static bool TryGetAgicBackendHostnameAnnotation(this V1Ingress ingress, ILog log, out string backendHostname)
        {
            if (IsAgicIngress(ingress)
                && ingress.Metadata.Annotations.TryGetValue(Constants.AgicBackendHostnameAnnotation, out backendHostname))
            {
                return true;
            }

            backendHostname = null;
            return false;
        }

        /// <summary>
        /// Returns a list of hosts for the ingressRoute.
        /// If routeInput from the same ingressRoute is passed, it returns the host for that specific routeInput
        /// </summary>
        /// <param name="ingressRoute">ingressRoute</param>
        /// <param name="routeInput">route from the above ingressRoute</param>
        /// <returns></returns>
        public static IEnumerable<string> GetIngressRouteHostForRoute(this IngressRoute ingressRoute, Route routeInput = null)
        {
            var hosts = new List<string>();

            foreach (var route in ingressRoute.Spec.Routes)
            {
                var hostValue = route.Match.GetHostRegexMatchValue();
                if (routeInput != null)
                {
                    if (routeInput == route)
                    {
                        return new List<string> { (string.IsNullOrWhiteSpace(hostValue)) ? string.Empty : hostValue };
                    }
                }
                else if (!string.IsNullOrWhiteSpace(hostValue))
                {
                    hosts.Add(hostValue);
                }
            }

            return hosts;
        }

        /// <summary>
        /// Input: IngressRoute's match field e.g. "Host(`example.com`) && PathPrefix(`/`)"
        /// </summary>
        /// <returns>"Host(`example.com`)"</returns>
        public static string GetHostRegexMatchValue(this string ingressRouteMatch)
        {
            var hostRegex = new Regex(@"Host\(`(.*?)`\)");
            var regexMatch = hostRegex.Matches(ingressRouteMatch).FirstOrDefault();
            return string.IsNullOrWhiteSpace(regexMatch?.Value) ? string.Empty : regexMatch.Value;
        }

        /// <summary>
        /// Input: ingressRoute's match field e.g. "Host(`example.com`) && PathPrefix(`/`)", "Host(`example.com`)", "user-1a2b"
        /// </summary>
        /// <returns>"Host(`user-1a2b.example.com`) && PathPrefix(`/`)"</returns>
        public static string GetUpdatedMatchHostValue(this string ingressRouteMatch, string oldHostMatch, string routeOnHeaderValue)
        {
            var oldHostValue = oldHostMatch.Replace("Host(`", string.Empty).Replace("`)", string.Empty);
            var newHostValue = $"{routeOnHeaderValue}.{oldHostValue}";
            return ingressRouteMatch.Replace(oldHostMatch, $"Host(`{newHostValue}`)");
        }

        /// <summary>
        /// Get feature flags set on devhostagent pod as an annotation
        /// </summary>
        /// <returns>Feature flags for ExP</returns>
        public static IEnumerable<string> GetFeatureFlags(this V1ObjectMeta metadata, ILog log)
        {
            var annotationValue = metadata.GetOptionalAnnotationValue(Routing.FeatureFlagsAnnotationName);

            if (string.IsNullOrEmpty(annotationValue))
            {
                return new List<string>();
            }

            var featureFlags = annotationValue.Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (featureFlags == null || !featureFlags.Any())
            {
                log.Error("Failed to read annotation value '{0}' from object '{1}': '{2}'. ", Routing.FeatureFlagsAnnotationName, new PII(metadata.Name), annotationValue);
                throw new RoutingException(Resources.FailedToReadAnnotationFormat, Routing.FeatureFlagsAnnotationName, metadata.Name);
            }

            return featureFlags;
        }

        #region Private methods

        private static string GetRequiredAnnotationValue(this V1ObjectMeta metadata, string annotationName, ILog log)
        {
            var value = metadata.GetOptionalAnnotationValue(annotationName);
            if (string.IsNullOrEmpty(value))
            {
                log.Error("Failed to read annotation value '{0}' from object '{1}'. ", annotationName, new PII(metadata.Name));
                throw new RoutingException(Resources.FailedToReadAnnotationFormat, annotationName, metadata.Name);
            }

            return value;
        }

        private static string GetOptionalAnnotationValue(this V1ObjectMeta metadata, string annotationName)
        {
            if (metadata?.Annotations != null
                && metadata.Annotations.TryGetValue(annotationName, out string annotationValue))
            {
                return annotationValue;
            }
            return string.Empty;
        }

        #endregion Private methods
    }
}