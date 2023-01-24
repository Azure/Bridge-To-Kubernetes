// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using FakeItEasy;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.RoutingManager.Envoy;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;
using System.Text.Json.Serialization;
using Xunit;
using System.Text.Json;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Tests
{
    public class EnvoyConfigBuilderTests
    {
        private ILog _log = A.Fake<ILog>();
        private EnvoyConfigBuilder _envoyConfigBuilder;

        public EnvoyConfigBuilderTests()
        {
            _envoyConfigBuilder = new EnvoyConfigBuilder(_log);
        }

        /// <summary>
        /// HelloWorld Service -> HelloWorldPod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_PodTrigger()
        {
            var triggerService = GetHelloTriggerService(HelloType.helloworld);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";
            var podTrigger = GetPodTriggerConfig(triggerService, routeOnHeaderValue, lpkPodName, lpkPodIp);
            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(triggerService, new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger }), new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloWorld Ingress -> HelloWorld Service -> HelloWorld Pod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_IngressTriggerA_PodTriggerA()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld);
            var helloWorldIngress = GetHelloTriggerIngress(HelloType.helloworld);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";
            var podTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue, lpkPodName, lpkPodIp);
            var ingressTriggers = GetIngressTriggerConfigs(helloWorldService, helloWorldIngress);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger }, ingressTriggers);

            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloWorld Ingress -> HelloWorld Service -> HelloWorld Pod(ToBeDebugged by 2 different users)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_IngressTriggerA_PodTriggerA_PodTriggerA()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld);
            var helloWorldIngress = GetHelloTriggerIngress(HelloType.helloworld);
            var routeOnHeaderValue1 = "user1";
            var routeOnHeaderValue2 = "user2";
            var lpkPodName1 = "lpkpodname1";
            var lpkPodName2 = "lpkpodname2";
            var lpkPodIp1 = "1.2.3.4";
            var lpkPodIp2 = "2.3.4.5";
            var podTrigger1 = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue1, lpkPodName1, lpkPodIp1);
            var podTrigger2 = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue2, lpkPodName2, lpkPodIp2);
            var ingressTriggers = GetIngressTriggerConfigs(helloWorldService, helloWorldIngress);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger1, podTrigger2 }, ingressTriggers);

            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger1, podTrigger2 });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloUniverse Ingress -> HelloUniverse Service -> HelloUniverse Pod(ToBeDebugged) -> HelloWorld Service -> HelloWorld Pod (To Be debugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_IngressTriggerA_PodTriggerA_PodTriggerB()
        {
            var helloUniverseService = GetHelloTriggerService(HelloType.hellouniverse);
            var helloUniverseIngress = GetHelloTriggerIngress(HelloType.hellouniverse);
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld);
            var routeOnHeaderValue1 = "user1";
            var routeOnHeaderValue2 = "user2";
            var lpkPodName1 = "lpkpodname1";
            var lpkPodName2 = "lpkpodname2";
            var lpkPodIp1 = "1.2.3.4";
            var lpkPodIp2 = "2.3.4.5";
            var helloUniversePodTrigger = GetPodTriggerConfig(helloUniverseService, routeOnHeaderValue1, lpkPodName1, lpkPodIp1);
            var helloWorldPodTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue2, lpkPodName2, lpkPodIp2);
            var ingressTriggers = GetIngressTriggerConfigs(helloUniverseService, helloUniverseIngress);

            // Check for envoy config for HelloUniverse Service
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { helloUniversePodTrigger }, ingressTriggers);
            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloUniverseService, routingStateEstablisherInput, new List<PodTriggerConfig> { helloUniversePodTrigger, helloWorldPodTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}_HelloUniverse.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }

            // Check for envoy config for HelloWorld Service
            routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { helloWorldPodTrigger });
            envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { helloUniversePodTrigger, helloWorldPodTrigger });
            retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}_HelloWorld.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloUniverse Ingress -> HelloUniverse Service -> HelloUniverse Pod -> HelloWorld Service -> HelloWorld Pod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_IngressTriggerA_PodTriggerB()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld);
            var helloUniverseService = GetHelloTriggerService(HelloType.hellouniverse);
            var helloUniverseIngress = GetHelloTriggerIngress(HelloType.hellouniverse);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";

            var podTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue, lpkPodName, lpkPodIp);

            // Check for envoy config for HelloUniverse Service
            var ingressTriggersForHelloUniverse = GetIngressTriggerConfigs(helloUniverseService, helloUniverseIngress);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(ingressTriggers: ingressTriggersForHelloUniverse);
            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloUniverseService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}_HelloUniverse.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }

            // Check for envoy config for HelloWorld Service
            routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger });
            envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}GetEnvoyConfig_PodTriggerB_HelloWorld.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloWorld Load Balancer Service -> HelloWorld Pod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_LoadBalancerTriggerA_PodTriggerA()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld, isLoadBalancer: true);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";
            var podTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue, lpkPodName, lpkPodIp);
            var lbTrigger = GetLoadBalancerTriggerConfig(HelloType.helloworld, helloWorldService);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger }, loadBalancerTrigger: lbTrigger);

            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloUniverse Load Balancer Service -> HelloUniverse Pod -> HelloWorld Service -> HelloWorld Pod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_LoadBalancerTriggerA_PodTriggerB()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld);
            var helloUniverseService = GetHelloTriggerService(HelloType.hellouniverse, isLoadBalancer: true);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";

            var podTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue, lpkPodName, lpkPodIp);

            // Check for envoy config for HelloUniverse Service
            var lbTriggersForHelloUniverse = GetLoadBalancerTriggerConfig(HelloType.hellouniverse, helloUniverseService);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(loadBalancerTrigger: lbTriggersForHelloUniverse);
            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloUniverseService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}_HelloUniverse.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }

            // Check for envoy config for HelloWorld Service
            routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger });
            envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}GetEnvoyConfig_PodTriggerB_HelloWorld.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloWorld Ingress -> HelloWorld Load Balancer Service -> HelloWorld Pod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_IngressTriggerA_LoadBalancerTriggerA_PodTriggerA()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld, isLoadBalancer: true);
            var helloWorldIngress = GetHelloTriggerIngress(HelloType.helloworld);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";
            var podTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue, lpkPodName, lpkPodIp);
            var lbTrigger = GetLoadBalancerTriggerConfig(HelloType.helloworld, helloWorldService);
            var ingressTriggers = GetIngressTriggerConfigs(helloWorldService, helloWorldIngress);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger }, ingressTriggers, loadBalancerTrigger: lbTrigger);

            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}{System.Reflection.MethodBase.GetCurrentMethod().Name}.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        /// <summary>
        /// HelloUniverse Ingress -> HelloUniverse Service -> HelloUniverse Pod -> HelloWorld Load Balancer Service -> HelloWorld Pod(ToBeDebugged)
        /// </summary>
        [Fact]
        public void GetEnvoyConfig_IngressTriggerA_LoadBalancerTriggerB_PodTriggerB()
        {
            var helloWorldService = GetHelloTriggerService(HelloType.helloworld, isLoadBalancer: true);
            var helloUniverseService = GetHelloTriggerService(HelloType.hellouniverse);
            var helloUniverseIngress = GetHelloTriggerIngress(HelloType.hellouniverse);
            var routeOnHeaderValue = "user1";
            var lpkPodName = "lpkpodname";
            var lpkPodIp = "1.2.3.4";

            var podTrigger = GetPodTriggerConfig(helloWorldService, routeOnHeaderValue, lpkPodName, lpkPodIp);

            // Check for envoy config for HelloUniverse Service
            var ingressTriggersForHelloUniverse = GetIngressTriggerConfigs(helloUniverseService, helloUniverseIngress);
            var routingStateEstablisherInput = new RoutingStateEstablisherInput(ingressTriggers: ingressTriggersForHelloUniverse);
            var envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloUniverseService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            var retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}GetEnvoyConfig_IngressTriggerA_PodTriggerB_HelloUniverse.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }

            // Check for envoy config for HelloWorld Service
            var lbTrigger = GetLoadBalancerTriggerConfig(HelloType.helloworld, helloWorldService);
            routingStateEstablisherInput = new RoutingStateEstablisherInput(new List<PodTriggerConfig> { podTrigger }, loadBalancerTrigger: lbTrigger);
            envoyConfig = _envoyConfigBuilder.GetEnvoyConfig(helloWorldService, routingStateEstablisherInput, new List<PodTriggerConfig> { podTrigger });
            retrievedConfig = JsonSerializer.Serialize(envoyConfig, new JsonSerializerOptions { WriteIndented = true, });
            using (var r = new StreamReader($"EnvoyConfigs{Path.DirectorySeparatorChar}GetEnvoyConfig_LoadBalancerTriggerA_PodTriggerA.txt"))
            {
                var expectedConfig = r.ReadToEnd().NormalizeLineBreaks();
                Assert.Equal(expectedConfig, retrievedConfig);
            }
        }

        private V1Ingress GetHelloTriggerIngress(HelloType helloType)
        {
            return new V1Ingress
            {
                Metadata = new V1ObjectMeta
                {
                    Name = $"{helloType}-ingress",
                    NamespaceProperty = "default"
                },
                Spec = new V1IngressSpec
                {
                    Rules = new List<V1IngressRule>
                    {
                        new V1IngressRule
                        {
                            Host = $"{helloType}.com",
                            Http = new V1HTTPIngressRuleValue
                            {
                                Paths = new List<V1HTTPIngressPath>
                                {
                                    new V1HTTPIngressPath
                                    {
                                        Path = "/",
                                        Backend = new V1IngressBackend
                                        {
                                            Service = new V1IngressServiceBackend
                                            {
                                                Name = $"{helloType}-service",
                                                Port = new V1ServiceBackendPort
                                                {
                                                    Number = 80
                                                }
                                            }
                                        }
                                    },
                                    new V1HTTPIngressPath
                                    {
                                        Path = "/hello",
                                        Backend = new V1IngressBackend
                                        {
                                            Service = new V1IngressServiceBackend
                                            {
                                                Name = $"{helloType}-service",
                                                Port = new V1ServiceBackendPort
                                                {
                                                    Number = 80
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        },
                        new V1IngressRule
                        {
                            Host = $"{helloType}again.com",
                            Http = new V1HTTPIngressRuleValue
                            {
                                Paths = new List<V1HTTPIngressPath>
                                {
                                    new V1HTTPIngressPath
                                    {
                                        Path = "/hello",
                                        Backend = new V1IngressBackend
                                        {
                                            Service = new V1IngressServiceBackend
                                            {
                                                Name = $"{helloType}-service",
                                                Port = new V1ServiceBackendPort
                                                {
                                                    Number = 80
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
        }

        private V1Service GetHelloTriggerService(HelloType helloType, bool isLoadBalancer = false)
        {
            return new V1Service
            {
                Metadata = new V1ObjectMeta
                {
                    Name = $"{helloType}-service",
                    NamespaceProperty = "default"
                },
                Spec = new V1ServiceSpec
                {
                    Selector = new Dictionary<string, string>
                    {
                        { "app", helloType.ToString() }
                    },
                    Ports = new List<V1ServicePort>
                    {
                        new V1ServicePort
                        {
                            Port = 80,
                            TargetPort = 80
                        }
                    },
                    Type = "LoadBalancer"
                }
            };
        }

        private List<IngressTriggerConfig> GetIngressTriggerConfigs(
            V1Service triggerService,
            V1Ingress triggerIngress)
        {
            var ingressTriggerConfigs = new List<IngressTriggerConfig>();
            foreach (var rule in triggerIngress.Spec.Rules)
            {
                foreach (var path in rule.Http.Paths)
                {
                    ingressTriggerConfigs.Add(
                        new IngressTriggerConfig(
                            namespaceName: "default",
                            triggerService: triggerService,
                            ingressName: triggerIngress.Metadata.Name,
                            servicePort: path.Backend.Service.Port.Number.Value,
                            host: rule.Host,
                            isAgicIngress: false,
                            agicBackendHostname: string.Empty,
                            httpReadinessProbe: null,
                            httpLivenessProbe: null));
                }
            }
            return ingressTriggerConfigs;
        }

        private LoadBalancerTriggerConfig GetLoadBalancerTriggerConfig(
            HelloType helloType,
            V1Service triggerService)
        {
            return new LoadBalancerTriggerConfig("default", triggerService, helloType.ToString(), false);
        }

        private PodTriggerConfig GetPodTriggerConfig(
            V1Service triggerService,
            string routeOnHeaderValue,
            string lpkPodName,
            string lpkPodIp)
        {
            return new PodTriggerConfig(
                    namespaceName: triggerService.Metadata.NamespaceProperty,
                    triggerService: triggerService,
                    lpkPodName: lpkPodName,
                    routeOnHeaderKey: Common.Constants.Routing.KubernetesRouteAsHeaderName,
                    routeOnHeaderValue: routeOnHeaderValue,
                    triggerPodIP: lpkPodIp,
                    correlationId: string.Empty);
        }

        private string DeserializeIndented(object obj)
        {
            var jsonSerializerOptions = new JsonSerializerOptions { WriteIndented = true, };

            return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true, });
        }
    }

    internal static class StringExtensions
    {
        internal static string NormalizeLineBreaks(this string content)
        {
            return content.Replace("\r\n", "\n").Replace("\n", System.Environment.NewLine);
        }
    }

    internal enum HelloType
    {
        helloworld,
        hellouniverse
    }
}