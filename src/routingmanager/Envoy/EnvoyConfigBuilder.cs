// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text.Json;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Envoy
{
    /// <summary>
    /// This class builds the config for envoy
    /// </summary>
    internal class EnvoyConfigBuilder
    {
        private readonly ILog _log;

        private static readonly string _httpConnectionManager = "envoy.http_connection_manager";
        private static readonly Regex _http1PortRegex = new Regex(@"^http(?!2)(-[a-z0-9_-]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex _http2GrpcPortRegex = new Regex(@"^(http2|grpc)(-[a-z0-9_-]+)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly string _serviceCloneWithPortsFormatString = "service_original_clone_{0}_{1}";
        private static readonly string _serviceStableWithHeaderWithPortsFormatString = "service_debug_withHeader_{0}_{1}_{2}_{3}";

        public EnvoyConfigBuilder(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Returns the envoy configuration based on trigger information
        /// </summary>
        public EnvoyConfig GetEnvoyConfig(V1Service triggerService, RoutingStateEstablisherInput routingStateEstablisherInput, IEnumerable<PodTriggerConfig> allPodTriggersInNamespace)
        {
            var envoyConfig = CreateEmptyEnvoyConfig();

            foreach (var servicePort in triggerService.Spec.Ports)
            {
                _log.Info("Configuring envoy for service '{0}' for port '{1}'", new PII(triggerService.Metadata.Name), new PII(servicePort.Port.ToString()));

                (var listener, var httpFilter) = AddListenerForHttp(envoyConfig, servicePort);

                InitializeListenerForIngressControllerLoadBalancer(routingStateEstablisherInput.LoadBalancerTrigger, servicePort, listener);

                var httpFilterVirtualHosts = httpFilter.TypedConfig.RouteConfig.VirtualHosts;

                // Configure for user-1a2b.example.com
                ConfigureVirtualHostForPrefixedHost(triggerService, routingStateEstablisherInput, allPodTriggersInNamespace, servicePort, httpFilterVirtualHosts);

                // Configure for user-1a2b.* (for ingresses with empty hosts or service of type load balancers)
                ConfigureVirtualHostForPrefixedMatchAllHost(triggerService, routingStateEstablisherInput, allPodTriggersInNamespace, servicePort, httpFilterVirtualHosts);

                // Configure for begin section - *
                ConfigureVirtualHostForMatchAllHost(triggerService, routingStateEstablisherInput.PodTriggers, servicePort, httpFilterVirtualHosts);

                // Now we will start adding clusters to this envoy configuration
                ConfigureClusters(triggerService, routingStateEstablisherInput.PodTriggers, envoyConfig, servicePort);
            }
            _log.Info("Envoy Config is: {0}", JsonSerializer.Serialize(envoyConfig));
            return envoyConfig;
        }

        private void ConfigureClusters(V1Service triggerService, IEnumerable<PodTriggerConfig> podTriggers, EnvoyConfig envoyConfig, V1ServicePort servicePort)
        {
            var portName = (servicePort.Name ?? string.Empty).ToLowerInvariant();
            var cloneCluster = new Cluster
            {
                Name = string.Format(_serviceCloneWithPortsFormatString, servicePort.Port, servicePort.TargetPort.Value),
                ConnectTimeout = "1.00s",
                Type = "strict_dns",
                LoadAssignment = new LoadAssignment
                {
                    ClusterName = string.Format(_serviceCloneWithPortsFormatString, servicePort.Port, servicePort.TargetPort.Value),
                    Endpoints = new List<EndpointElement>
                    {
                        new EndpointElement
                        {
                            LbEndpoints = new List<LbEndpoint>
                            {
                                new LbEndpoint
                                {
                                    Endpoint = new LbEndpointEndpoint
                                    {
                                        Address = new EndpointAddress
                                        {
                                            SocketAddress = new SocketAddress
                                            {
                                                Address = $"{KubernetesUtilities.GetKubernetesResourceNamePreserveSuffix(triggerService.Metadata.Name, $"{Constants.ClonedSuffix}-svc")}.{triggerService.Metadata.NamespaceProperty}",
                                                // envoy listens on the target port but sends the request to the source port
                                                // of that target so that recepient service can then send it to the target port
                                                PortValue = servicePort.Port
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            if (_http2GrpcPortRegex.IsMatch(portName))
            {
                cloneCluster.Http2ProtocolOptions = new object();
            }
            else if (_http1PortRegex.IsMatch(portName))
            {
                cloneCluster.Http1ProtocolOptions = new object();
            }

            envoyConfig.StaticResources.Clusters.Add(cloneCluster);

            foreach (var podTrigger in podTriggers)
            {
                var routeCluster = new Cluster
                {
                    Name = string.Format(_serviceStableWithHeaderWithPortsFormatString, podTrigger.RouteOnHeader.Key, podTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value),
                    ConnectTimeout = "1.00s",
                    Type = "static",
                    LoadAssignment = new LoadAssignment
                    {
                        ClusterName = string.Format(_serviceStableWithHeaderWithPortsFormatString, podTrigger.RouteOnHeader.Key, podTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value),
                        Endpoints = new List<EndpointElement>
                        {
                            new EndpointElement
                            {
                                LbEndpoints = new List<LbEndpoint>
                                {
                                    {
                                        new LbEndpoint
                                        {
                                            Endpoint = new LbEndpointEndpoint
                                            {
                                                Address = new EndpointAddress
                                                {
                                                    SocketAddress = new SocketAddress
                                                    {
                                                        Address = podTrigger.TriggerPodIp,
                                                        // envoy listens on the target port and sends the request on the target port itself
                                                        // because we are directly sending the request to the pod
                                                        PortValue = long.Parse(servicePort.TargetPort.Value)
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                };

                if (_http2GrpcPortRegex.IsMatch(portName))
                {
                    routeCluster.Http2ProtocolOptions = new object();
                }
                else if (_http1PortRegex.IsMatch(portName))
                {
                    routeCluster.Http1ProtocolOptions = new object();
                }

                envoyConfig.StaticResources.Clusters.Add(routeCluster);
            }
        }

        private void ConfigureVirtualHostForMatchAllHost(V1Service triggerService, IEnumerable<PodTriggerConfig> podTriggers, V1ServicePort servicePort, IList<VirtualHost> httpFilterVirtualHosts)
        {
            // Add the entry for default * domain for all pod triggers. Populate routes for this in the next loop.
            httpFilterVirtualHosts.Add(
                    new VirtualHost
                    {
                        Name = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}_route_default",
                        Domains = new List<string> { "*" },
                        Routes = new List<RouteElement>()
                    });

            foreach (var inputPodTrigger in podTriggers)
            {
                _log.Info("Configuring envoy for service '{0}' for port '{1}' for * host : Pod trigger : '{2}'", new PII(triggerService.Metadata.Name), new PII(servicePort.Port.ToString()), new PII(inputPodTrigger.TriggerEntityName));

                // Add to the last virtual host for default * domain
                httpFilterVirtualHosts.Last().Routes.Add(
                    new RouteElement
                    {
                        Match = new Match
                        {
                            Prefix = "/",
                            Headers = new List<Header>
                            {
                                new Header
                                {
                                    Name = inputPodTrigger.RouteOnHeader.Key,
                                    Value = inputPodTrigger.RouteOnHeader.Value
                                }
                            }
                        },
                        Route = new Route
                        {
                            Cluster = string.Format(_serviceStableWithHeaderWithPortsFormatString, inputPodTrigger.RouteOnHeader.Key, inputPodTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value)
                        }
                    });
            }

            // Add the default match-all rule to the last virtual host for default * domain
            httpFilterVirtualHosts.Last().Routes.Add(
                new RouteElement
                {
                    Match = new Match
                    {
                        Prefix = "/"
                    },
                    Route = new Route
                    {
                        Cluster = string.Format(_serviceCloneWithPortsFormatString, servicePort.Port, servicePort.TargetPort.Value)
                    }
                });
        }

        private void ConfigureVirtualHostForPrefixedMatchAllHost(V1Service triggerService, RoutingStateEstablisherInput routingStateEstablisherInput, IEnumerable<PodTriggerConfig> allPodTriggersInNamespace, V1ServicePort servicePort, IList<VirtualHost> httpFilterVirtualHosts)
        {
            if (routingStateEstablisherInput.IngressTriggers.Any(ingressTrigger => string.IsNullOrWhiteSpace(ingressTrigger.Host) && string.IsNullOrWhiteSpace(ingressTrigger.AgicBackendHostName))
                                || routingStateEstablisherInput.LoadBalancerTrigger != null)
            {
                foreach (var podTrigger in allPodTriggersInNamespace)
                {
                    // If we have already configured virtual host for another pod trigger with the same route on header value, so skipping this pod trigger
                    var existingVirtualHost = httpFilterVirtualHosts.Where(virtualHost => FirstDomainStartsWithRoutingHeaderValue(virtualHost.Domains, podTrigger.RouteOnHeader.Value)).FirstOrDefault();
                    if (existingVirtualHost != null)
                    {
                        // Handle the case where two same routing header value are present for podTrigger
                        if (routingStateEstablisherInput.PodTriggers != null && routingStateEstablisherInput.PodTriggers.Contains(podTrigger))
                        {
                            // This is safe to assume RouteElement and a Cluster are not null as they are defined below.
                            existingVirtualHost.Routes.First().Route.Cluster = string.Format(_serviceStableWithHeaderWithPortsFormatString, podTrigger.RouteOnHeader.Key, podTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value);
                        }
                        else
                        {
                            _log.Info("While configuring envoy for ingress triggers, Another pod trigger with the same routing header value '{0}' was already used for configuring the ingress triggers, so skipping this pod trigger"
                            , new PII(podTrigger.RouteOnHeader.Value));
                        }
                        continue;
                    }

                    _log.Info("Configuring envoy for service '{0}' for port '{1}' for empty host : Pod trigger : '{2}'", new PII(triggerService.Metadata.Name), new PII(servicePort.Port.ToString()), new PII(podTrigger.TriggerEntityName));

                    var domains = new List<string> { $"{podTrigger.RouteOnHeader.Value}.*" };
                    var ingressVirtualHost =
                        new VirtualHost
                        {
                            // We are only adding the rule for domain starting with routing header. We are not adding the catch all * here on purpose.
                            // This is because there is at least one pod trigger for sure. We will be adding the * catch all rule as part of that.
                            Name = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}_route_ingress_withDomain_{string.Join(',', domains)}",
                            Domains = domains,
                            Routes = new List<RouteElement>
                            {
                                new RouteElement
                                {
                                    Match = new Match
                                    {
                                        Prefix = "/"
                                    },
                                    // Will add Route below
                                    RequestHeadersToAdd = new List<RequestHeadersToAdd>
                                    {
                                        new RequestHeadersToAdd
                                        {
                                            Header = new RequestHeaderToAdd
                                            {
                                                Key = podTrigger.RouteOnHeader.Key,
                                                Value = podTrigger.RouteOnHeader.Value
                                            },
                                            Append = false
                                        }
                                    }
                                }
                            }
                        };

                    if (routingStateEstablisherInput.PodTriggers != null && routingStateEstablisherInput.PodTriggers.Contains(podTrigger))
                    {
                        // This means input.TriggerService has an ingress and pod trigger both, so we need to route to service_stable for the given domains
                        ingressVirtualHost.Routes.First().Route = new Route
                        {
                            Cluster = string.Format(_serviceStableWithHeaderWithPortsFormatString, podTrigger.RouteOnHeader.Key, podTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value)
                        };
                    }
                    else
                    {
                        ingressVirtualHost.Routes.First().Route = new Route
                        {
                            Cluster = string.Format(_serviceCloneWithPortsFormatString, servicePort.Port, servicePort.TargetPort.Value)
                        };
                    }

                    // Add to the last added listener above. There is always only a single Filter chain and a single filter in that.
                    httpFilterVirtualHosts.Add(ingressVirtualHost);
                }
            }
        }

        private void ConfigureVirtualHostForPrefixedHost(V1Service triggerService, RoutingStateEstablisherInput routingStateEstablisherInput, IEnumerable<PodTriggerConfig> allPodTriggersInNamespace, V1ServicePort servicePort, IList<VirtualHost> httpFilterVirtualHosts)
        {
            foreach (var ingressTrigger in routingStateEstablisherInput.IngressTriggers)
            {
                if (Constants.WildcardHostRegex.IsMatch(ingressTrigger.Host))
                {
                    // Ignore wildcard hosts
                    _log.Verbose("Ignoring wilcard ingress {0}", new PII(ingressTrigger.Host));
                    continue;
                }

                foreach (var podTrigger in allPodTriggersInNamespace)
                {
                    if (servicePort.Port != ingressTrigger.ServicePort
                        // We will be adding rules for ingresses with empty hosts later
                        || (string.IsNullOrWhiteSpace(ingressTrigger.Host) && string.IsNullOrWhiteSpace(ingressTrigger.AgicBackendHostName)))
                    {
                        continue;
                    }

                    var prefixedHost = GetModifiedHost(
                                        string.IsNullOrWhiteSpace(ingressTrigger.AgicBackendHostName) ? ingressTrigger.Host : ingressTrigger.AgicBackendHostName,
                                        podTrigger.RouteOnHeader.Value);
                    _log.Info("Configuring envoy for service '{0}' for port '{1}' : Ingress trigger : '{2}' and pod trigger with route on header value : '{3}'. Host: '{4}'"
                        , new PII(triggerService.Metadata.Name), new PII(servicePort.Port.ToString()), new PII(ingressTrigger.TriggerEntityName), new PII(podTrigger.RouteOnHeader.Value), new PII(prefixedHost));

                    var existingVirtualHost = httpFilterVirtualHosts.Where(vh => vh.Domains.Contains(prefixedHost)).FirstOrDefault();
                    if (existingVirtualHost != null)
                    {
                        if (routingStateEstablisherInput.PodTriggers != null && routingStateEstablisherInput.PodTriggers.Contains(podTrigger))
                        {
                            // This is safe to assume RouteElement and a Cluster are not null as they are defined below.
                            existingVirtualHost.Routes.First().Route.Cluster = string.Format(_serviceStableWithHeaderWithPortsFormatString, podTrigger.RouteOnHeader.Key, podTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value);
                        }
                        else
                        {
                            _log.Info("Virtual host for this ingress trigger has already been configured or not required. Skipping.");
                        }
                        continue;
                    }

                    var domains = new List<string> { prefixedHost };
                    var ingressVirtualHost =
                        new VirtualHost
                        {
                            // We are only adding the rule for domain starting with routing header. We are not adding the catch all * here on purpose.
                            // This is because there is at least one pod trigger for sure. We will be adding the * catch all rule as part of that.
                            Name = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}_route_ingress_withDomain_{string.Join(',', domains)}",
                            Domains = domains,
                            Routes = new List<RouteElement>
                            {
                                new RouteElement
                                {
                                    Match = new Match
                                    {
                                        Prefix = "/"
                                    },
                                    // Will add Route below
                                    RequestHeadersToAdd = new List<RequestHeadersToAdd>
                                    {
                                        new RequestHeadersToAdd
                                        {
                                            Header = new RequestHeaderToAdd
                                            {
                                                Key = podTrigger.RouteOnHeader.Key,
                                                Value = podTrigger.RouteOnHeader.Value
                                            },
                                            Append = false
                                        }
                                    }
                                }
                            }
                        };

                    if (routingStateEstablisherInput.PodTriggers != null && routingStateEstablisherInput.PodTriggers.Contains(podTrigger))
                    {
                        // This means input.TriggerService has an ingress and pod trigger both, so we need to route to service_stable for the given domains
                        ingressVirtualHost.Routes.First().Route = new Route
                        {
                            Cluster = string.Format(_serviceStableWithHeaderWithPortsFormatString, podTrigger.RouteOnHeader.Key, podTrigger.RouteOnHeader.Value, servicePort.Port, servicePort.TargetPort.Value)
                        };
                    }
                    else
                    {
                        ingressVirtualHost.Routes.First().Route = new Route
                        {
                            Cluster = string.Format(_serviceCloneWithPortsFormatString, servicePort.Port, servicePort.TargetPort.Value)
                        };
                    }

                    // Add to the last added listener above. There is always only a single Filter chain and a single filter in that.
                    httpFilterVirtualHosts.Add(ingressVirtualHost);
                }
            }
        }

        private void InitializeListenerForIngressControllerLoadBalancer(LoadBalancerTriggerConfig loadBalancerTrigger, V1ServicePort servicePort, Listener listener)
        {
            if (loadBalancerTrigger != null && loadBalancerTrigger.IsIngressController)
            {
                listener.ListenerFilters.Add(
                    new ListenerFilter
                    {
                        Name = "envoy.filters.listener.tls_inspector"
                    });

                listener.FilterChains.Insert(0,
                    new FilterChain
                    {
                        FilterChainMatch = new FilterChainMatch
                        {
                            TransportProtocol = "tls"
                        },
                        Filters = new List<Filter>
                        {
                            new Filter
                            {
                                Name = "envoy.tcp_proxy",
                                Config = new Filter_Config
                                {
                                    Stat_Prefix = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}_tls",
                                    Cluster = string.Format(_serviceCloneWithPortsFormatString, servicePort.Port, servicePort.TargetPort.Value)
                                }
                            }
                        }
                    });
                _log.Info("Configured for ingress controller");
            }
        }

        private (Listener listener, Filter httpFilter) AddListenerForHttp(EnvoyConfig envoyConfig, V1ServicePort servicePort)
        {
            var httpFilter =
                new Filter
                {
                    Name = _httpConnectionManager,
                    TypedConfig = new TypedConfig
                    {
                        Type = "type.googleapis.com/envoy.extensions.filters.network.http_connection_manager.v3.HttpConnectionManager",
                        CodecType = "auto",
                        StatPrefix = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}",
                        HttpFilters = new List<HttpFilter>
                        {
                            new HttpFilter
                            {
                                Name = "envoy.filters.http.router"
                            }
                        },
                        RouteConfig = new RouteConfig
                        {
                            Name = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}_route",
                            VirtualHosts = new List<VirtualHost>()
                        }
                    }
                };

            var listener =
                new Listener
                {
                    Name = $"listener_{servicePort.Port}_{servicePort.TargetPort.Value}",
                    Address = new ListenerAddress
                    {
                        SocketAddress = new SocketAddress
                        {
                            Address = "0.0.0.0",
                            PortValue = int.Parse(servicePort.TargetPort.Value)
                        }
                    },
                    ListenerFilters = new List<ListenerFilter>
                    {
                        new ListenerFilter
                        {
                            Name = "envoy.filters.listener.http_inspector"
                        }
                    },
                    FilterChains = new List<FilterChain>
                    {
                        new FilterChain
                        {
                            FilterChainMatch = new FilterChainMatch
                            {
                                ApplicationProtocols = new List<string>
                                {
                                    "http/1.0",
                                    "http/1.1",
                                    "h2c"
                                }
                            },
                            Filters = new List<Filter>
                            {
                                httpFilter
                            }
                        }
                    }
                };

            envoyConfig.StaticResources.Listeners.Add(listener);
            return (listener, httpFilter);
        }

        private EnvoyConfig CreateEmptyEnvoyConfig()
        {
            return new EnvoyConfig
            {
                Admin = new Admin
                {
                    AccessLogPath = "/tmp/admin_access.log"
                },
                StaticResources = new StaticResources
                {
                    Listeners = new List<Listener>(),
                    Clusters = new List<Cluster>()
                }
            };
        }

        /// <summary>
        /// Get host with routing header as prefix
        /// </summary>
        private string GetModifiedHost(string host, string routeOnHeaderValue)
        {
            return $"{routeOnHeaderValue}.{host}";
        }

        /// <summary>
        /// Check if the first domain in list of domains starts with the route on header value
        /// </summary>
        private bool FirstDomainStartsWithRoutingHeaderValue(IEnumerable<string> domains, string routeOnHeaderValue)
        {
            if (domains == null || domains.Count() != 1)
            {
                _log.Error("Count of Virtual hosts list is not equal to 1 which is unexpected.");
                throw new RoutingException(Resources.VirtualHostCount);
            }

            var virtualHostSplit = domains.First().Split(".");
            if (virtualHostSplit.Count() < 2)
            {
                _log.Error("Virtual host does not contain any '.' which is unexpected. ");
                throw new RoutingException(Resources.VirtualHostInvalid);
            }

            return StringComparer.OrdinalIgnoreCase.Equals(virtualHostSplit.First(), routeOnHeaderValue);
        }
    }
}