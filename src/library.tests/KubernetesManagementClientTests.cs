// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Autofac;
using FakeItEasy;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class KubernetesManagementClientTests : TestsBase
    {
        private IKubernetesManagementClient _kubeManagementClient;
        private List<V1Namespace> _sampleV1Namespaces;
        private const string aksKubeConfig = @"apiVersion: v1
clusters:
- cluster:
    certificate-authority-data: xxxx
    server: https://april8prod-michellerg-38c655-01258d23.hcp.eastus.azmk8s.io:443
  name: April8Prod
contexts:
- context:
    cluster: April8Prod
    user: clusterUser_michellerg_April8Prod
  name: April8Prod
current-context: April8Prod
kind: Config
preferences: {}
users:
- name: clusterUser_michellerg_April8Prod
  user:
    client-certificate-data: xxxx
    client-key-data: xxxx
    token: xxxx";

        private const string nonAksKubeConfig = @"apiVersion: v1
clusters:
- cluster:
    certificate-authority-data: xxxx
    server: https://april8prod-michellerg-38c655-01258d23.hcp.eastus.some-other-provider.io:443
  name: April8Prod
contexts:
- context:
    cluster: April8Prod
    user: clusterUser_michellerg_April8Prod
  name: April8Prod
current-context: April8Prod
kind: Config
preferences: {}
users:
- name: clusterUser_michellerg_April8Prod
  user:
    client-certificate-data: xxxx
    client-key-data: xxxx
    token: xxxx";

        public KubernetesManagementClientTests()
        {
            A.CallTo(() => _autoFake.Resolve<IK8sClientFactory>().LoadKubeConfig(default(Stream))).WithAnyArguments().Returns(null);
            _kubeManagementClient = _autoFake.Resolve<KubernetesManagementClient>(new NamedParameter("kubeConfigContent", "myKubeConfig"),
                                                                                    new NamedParameter("userAgent", "myUserAgent"),
                                                                                    new NamedParameter("correlationId", "myCorrelationId"));

            _sampleV1Namespaces = new List<V1Namespace>
            {
                new V1Namespace() { Metadata = new V1ObjectMeta() { Name = "namespace1" } },
                new V1Namespace() { Metadata = new V1ObjectMeta() { Name = "namespace2" } }
            };
        }

        [Fact]
        public void ListNamespacesAsync()
        {
            var expectedSpaces = new List<string> { "namespace1", "namespace2" };
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListNamespacesAsync(default, default)).WithAnyArguments().Returns(new V1NamespaceList(_sampleV1Namespaces));
            var resultSpaces = _kubeManagementClient.ListNamespacesAsync(cancellationToken: default).Result;

            Assert.Equal(expectedSpaces.OrderBy(x => x), resultSpaces.Value.OrderBy(x => x));
        }

        [Fact]
        public void ListNamespacesAsyncExcludeRestricted()
        {
            _sampleV1Namespaces.Add(new V1Namespace() { Metadata = new V1ObjectMeta() { Name = "azure-system" } });
            var expectedSpaces = new List<string> { "namespace1", "namespace2" };

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListNamespacesAsync(default, default)).WithAnyArguments().Returns(new V1NamespaceList(_sampleV1Namespaces));
            var resultSpaces = _kubeManagementClient.ListNamespacesAsync(cancellationToken: default, excludeReservedNamespaces: true).Result;

            Assert.Equal(expectedSpaces.OrderBy(x => x), resultSpaces.Value.OrderBy(x => x));
        }

        [Fact]
        public void ListNamespacesAsyncIncludeRestricted()
        {
            _sampleV1Namespaces.Add(new V1Namespace() { Metadata = new V1ObjectMeta() { Name = "azure-system" } });
            var expectedSpaces = new List<string> { "namespace1", "namespace2", "azure-system" };

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListNamespacesAsync(default, default)).WithAnyArguments().Returns(new V1NamespaceList(_sampleV1Namespaces));
            var resultSpaces = _kubeManagementClient.ListNamespacesAsync(cancellationToken: default, excludeReservedNamespaces: false).Result;

            Assert.Equal(expectedSpaces.OrderBy(x => x), resultSpaces.Value.OrderBy(x => x));
        }

        [Fact]
        public async void ListPublicUrlsAsync()
        {
            var ingress1 = new V1Ingress
            {
                Metadata = new V1ObjectMeta(),
                Spec = new V1IngressSpec
                {
                    Rules = new List<V1IngressRule>
                    {
                        new V1IngressRule
                        {
                            Host = "apple.com",
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
                                                Name = "apple",
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
                            Host = "pear.com",
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
                                                Name = "pear",
                                                Port = new V1ServiceBackendPort
                                                {
                                                    Number = 81
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    },
                    Tls = new List<V1IngressTLS>
                    {
                        new V1IngressTLS
                        {
                            Hosts = new List<string> { "pear.com" },
                            SecretName = "pear-secret"
                        }
                    }
                }
            };

            var ingress2 = new V1Ingress
            {
                Metadata = new V1ObjectMeta(),
                Spec = new V1IngressSpec
                {
                    Rules = new List<V1IngressRule>
                    {
                        new V1IngressRule
                        {
                            Host = "grapes.com",
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
                                                Name = "grapes",
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

            var lb = new V1Service
            {
                Spec = new V1ServiceSpec
                {
                    Type = "LoadBalancer"
                },
                Status = new V1ServiceStatus
                {
                    LoadBalancer = new V1LoadBalancerStatus
                    {
                        Ingress = new List<V1LoadBalancerIngress>
                        {
                            new V1LoadBalancerIngress
                            {
                                Ip = "1.2.3.4"
                            }
                        }
                    }
                }
            };

            var ingressList = new List<V1Ingress>() { ingress1, ingress2 };
            var lbList = new List<V1Service>();
            lbList.Add(lb);

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListIngressesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new V1IngressList(ingressList));
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListLoadBalancerServicesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new List<V1Service>(lbList));

            var expectedUrls = new List<string> { "http://jondoe.apple.com/", "https://jondoe.pear.com/", "http://jondoe.grapes.com/", "http://jondoe.1.2.3.4.nip.io/" };
            var urls = await _kubeManagementClient.ListPublicUrlsInNamespaceAsync("default", CancellationToken.None, "jondoe");
            Assert.NotNull(urls);
            Assert.Equal(4, urls.Value.Count());
            foreach (var url in urls.Value)
            {
                Assert.Contains(url.AbsoluteUri, expectedUrls);
            }
        }

        [Fact]
        public async void ListPublicUrlsAsync_DockerDesktop()
        {
            var lb1 = new V1Service
            {
                Spec = new V1ServiceSpec
                {
                    Type = "LoadBalancer"
                },
                Status = new V1ServiceStatus
                {
                    LoadBalancer = new V1LoadBalancerStatus
                    {
                        Ingress = new List<V1LoadBalancerIngress>
                        {
                            new V1LoadBalancerIngress
                            {
                                Ip = "1.2.3.4"
                            }
                        }
                    }
                }
            };

            var lb2 = new V1Service
            {
                Spec = new V1ServiceSpec
                {
                    Type = "LoadBalancer"
                },
                Status = new V1ServiceStatus
                {
                    LoadBalancer = new V1LoadBalancerStatus
                    {
                        Ingress = new List<V1LoadBalancerIngress>
                        {
                            new V1LoadBalancerIngress
                            {
                                Hostname = "localhost"
                            }
                        }
                    }
                }
            };

            var lbList = new List<V1Service>();
            lbList.Add(lb1);
            lbList.Add(lb2);

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListIngressesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new V1IngressList() { Items = new List<V1Ingress>() });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListLoadBalancerServicesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new List<V1Service>(lbList));

            var expectedUrls = new List<string> { "http://jondoe.1.2.3.4.nip.io/", "http://jondoe.localhost/" };
            var urls = await _kubeManagementClient.ListPublicUrlsInNamespaceAsync("default", CancellationToken.None, "jondoe");
            Assert.NotNull(urls);
            Assert.Equal(expectedUrls.Count, urls.Value.Count());
            foreach (var url in urls.Value)
            {
                Assert.Contains(url.AbsoluteUri, expectedUrls);
            }
        }

        [Fact]
        public async void ListPublicUrls_NullRef_EmptyPathAsync()
        {
            var ingress1 = new V1Ingress
            {
                Metadata = new V1ObjectMeta(),
                Spec = new V1IngressSpec
                {
                    Rules = new List<V1IngressRule>
                    {
                        new V1IngressRule
                        {
                            Host = "apple.com",
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
                                                Name = "apple",
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
                            Host = "pear.com",
                            Http = new V1HTTPIngressRuleValue
                            {
                                Paths = new List<V1HTTPIngressPath>
                                {
                                    null
                                }
                            }
                        }
                    }
                }
            };

            var ingress2 = new V1Ingress
            {
                Metadata = new V1ObjectMeta(),
                Spec = new V1IngressSpec
                {
                    Rules = new List<V1IngressRule>
                    {
                        null,
                        new V1IngressRule
                        {
                            Host = "grapes.com",
                            Http = new V1HTTPIngressRuleValue
                            {
                                Paths = new List<V1HTTPIngressPath>
                                {
                                    new V1HTTPIngressPath
                                    {
                                        Path = null,
                                        Backend = new V1IngressBackend
                                        {
                                            Service = new V1IngressServiceBackend
                                            {
                                                Name = "grapes",
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
                    },
                    Tls = new List<V1IngressTLS>
                    {
                        new V1IngressTLS
                        {
                            Hosts = new List<string> { "grapes.com" },
                            SecretName = "grapes-secret"
                        }
                    }
                }
            };

            var ingressList = new List<V1Ingress>() { ingress1, ingress2 };

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListIngressesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new V1IngressList(ingressList));
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListLoadBalancerServicesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new List<V1Service>());

            var expectedUrls = new List<string> { "http://jondoe.apple.com/", "https://jondoe.grapes.com/" };
            var urls = await _kubeManagementClient.ListPublicUrlsInNamespaceAsync("default", CancellationToken.None, "jondoe");
            Assert.NotNull(urls);
            Assert.Equal(expectedUrls.Count, urls.Value.Count());
            foreach (var url in urls.Value)
            {
                Assert.Contains(url.AbsoluteUri, expectedUrls);
            }
        }
    }
}