// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using Autofac;
using FakeItEasy;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class RoutingManagementClientTests : TestsBase
    {
        private IRoutingManagementClient _routingManagementClient;

        public RoutingManagementClientTests()
        {
            _routingManagementClient = _autoFake.Resolve<RoutingManagementClient>(new NamedParameter("namespaceName", ""),
                                                                                  new NamedParameter("userAgent", "myUserAgent"),
                                                                                  new NamedParameter("correlationId", "myCorrelationId"));
        }

        [Fact]
        public async void GetValidationError_Positive()
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

            var ingressList = new List<V1Ingress>() { ingress1 };

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListIngressesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new V1IngressList(ingressList));
            var errors = await _routingManagementClient.GetValidationErrorsAsync("jondoe", CancellationToken.None);
            Assert.NotNull(errors);
            Assert.Equal(string.Empty, errors.Value);
        }

        [Fact]
        public async void GetValidationError_Negative()
        {
            var host = new string('b', Constants.Https.LetsEncryptMaxDomainLength + 1);
            var ingress1 = new V1Ingress
            {
                Metadata = new V1ObjectMeta
                {
                    Annotations = new Dictionary<string, string>
                    {
                        { Constants.Https.CertManagerAnnotationKey, Constants.Https.LetsEncryptAnnotationValue }
                    }
                },
                Spec = new V1IngressSpec
                {
                    Rules = new List<V1IngressRule>
                    {
                        new V1IngressRule
                        {
                            Host = host,
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
                    },
                    Tls = new List<V1IngressTLS>
                    {
                        new V1IngressTLS
                        {
                            Hosts = new List<string> { host },
                            SecretName = "apple-secret"
                        }
                    }
                }
            };

            var ingressList = new List<V1Ingress>() { ingress1 };

            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListIngressesInNamespaceAsync(default, default)).WithAnyArguments().Returns(new V1IngressList(ingressList));
            var errors = await _routingManagementClient.GetValidationErrorsAsync("jondoe", CancellationToken.None);
            Assert.NotNull(errors);
            Assert.Contains("Please reduce the length of your isolation header or your domain host.", errors.Value);
        }
    }
}