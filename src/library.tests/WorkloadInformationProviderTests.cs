// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Autofac;
using FakeItEasy;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class WorkloadInformationProviderTests : TestsBase
    {
        private IWorkloadInformationProvider _workloadInformationProvider;

        public WorkloadInformationProviderTests()
        {
            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(async () => _autoFake.Resolve<RemoteContainerConnectionDetails>());
            _workloadInformationProvider = _autoFake.Resolve<WorkloadInformationProvider>(TypedParameter.From(remoteContainerConnectionDetails));
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(5, 20)]
        [InlineData(10, 3)]
        [InlineData(1, 3)]
        public async void GetReachableServicesAsync_HeadlessService(int numServices, int numAddresses)
        {
            ConfigureHeadlessService(numServices: numServices, namingFunction: (i) => $"myapp-{i}", numAddresses: numAddresses, addressHostNamingFunction: (i) => $"Host-{i}");
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));
            // Doing numServices-1 when calculating because we are adding empty subset for one service and that will be skipped
            Assert.Equal((numServices-1) * (numAddresses), result.Count());
            foreach (var endpoint in result) {
                if (endpoint.Ports.Any()) {
                    Assert.Equal(endpoint.Ports.ElementAt(0).LocalPort, -1);
                }
            }
            
        }

        [Theory]
        [InlineData(5, 1)]
        public async void GetReachableServicesAsync_PortsToIgnore_HeadlessService(int numServices, int numAddresses)
        { 
            // The ports provided to the service
            List<int> portsOffered = new List<int> {300, 450, 180, 190, 312};
            // The ports which should be ignored by the service
            List<int> portsToIgnore = new List<int> {190, 300};
            // The ports which were allocated by the service, these must not include any of the ignore ports
            List<int> portsAllocated = new List<int> {450, 180, 312};
            
            ConfigureHeadlessService(numServices: numServices, namingFunction: (i) => $"myapp-{i}", numAddresses: numAddresses, addressHostNamingFunction: (i) => $"Host-{i}", "190, 300", portsOffered);           
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));                       
            List<int> portsInResult = new List<int>();
            foreach (var endpoint in result)
            {
                if (endpoint.Ports.Any())
                {
                    // None of the ports to ignore must be used 
                    Assert.Empty(endpoint.Ports.Where(p => portsToIgnore.Contains(p.RemotePort)));                    
                    portsInResult.AddRange(endpoint.Ports.Select( p => p.RemotePort));
                }
            }
            Assert.True(portsAllocated.All(p => portsInResult.Contains(p)));
        }

        [Theory]
        [InlineData(5, 1)]
        public async void GetReachableServicesAsync_NoPortsToIgnore_HeadlessService(int numServices, int numAddresses)
        { 
            // The ports provided to the service
            List<int> portsOffered = new List<int> {300, 450, 180, 190, 312};            
            ConfigureHeadlessService(numServices: numServices, namingFunction: (i) => $"myapp-{i}", numAddresses: numAddresses, addressHostNamingFunction: (i) => $"Host-{i}", "", portsOffered);            
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));                       
            List<int> portsInResult = new List<int>();
            foreach (var endpoint in result)
            {
                if (endpoint.Ports.Any())
                {
                    portsInResult.AddRange(endpoint.Ports.Select( p => p.RemotePort));
                }
            }
            Assert.True(portsInResult.All(p => portsOffered.Contains(p)));
        }

        [Theory]
        [InlineData(5, 20)]
        public async void GetReachableServicesAsync_PortsToIgnoreIncorrectFormat_HeadlessService(int numServices, int numAddresses)
        { 
            ConfigureHeadlessService(numServices: numServices, namingFunction: (i) => $"myapp-{i}", numAddresses: numAddresses, addressHostNamingFunction: (i) => $"Host-{i}", "ajhfja, 19o");            
            await Assert.ThrowsAsync<UserVisibleException>(() => _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken)));                       
        }

        private void ConfigureHeadlessService(int numServices, Func<int, string> namingFunction, int numAddresses, Func<int, string> addressHostNamingFunction, string ignorePorts = null, List<int> ports = null)
        {
            var serviceList = new List<V1Service>();
            // introducing this variable so we can add an endpoint with empty subset to have crash coverage
            bool addSubset = false;
            for (int i = 0; i < numServices; i++)
            {
                var service = new V1Service()
                {
                    Spec = new V1ServiceSpec()
                    {
                        Type = "ClusterIP",
                        ClusterIP = "None",
                        Ports = new List<V1ServicePort> { new V1ServicePort(port: ports?.ElementAtOrDefault(i) ?? 80, protocol: "TCP") },
                        Selector = new Dictionary<string, string> { { "app", "myapp" } }
                    },
                    Metadata = new V1ObjectMeta()
                    {
                        Name = namingFunction(i),
                    }
                };

                if (!string.IsNullOrEmpty(ignorePorts))
                {
                    service.Metadata.Annotations = new Dictionary<string, string>();
                    service.Metadata.Annotations.Add("bridgetokubernetes/ignore-ports", ignorePorts);
                }

                serviceList.Add(service);
                var subsets = new List<V1EndpointSubset>()
                        {
                            new V1EndpointSubset()
                            {
                                Ports = new List<Corev1EndpointPort> { new Corev1EndpointPort(port: ports?.ElementAtOrDefault(i) ?? 80, protocol: "TCP") },
                                Addresses =  new List<V1EndpointAddress>()
                            }
                        };
                if (!addSubset)
                {
                    subsets = null;
                }
                var endPoint = new V1Endpoints()
                {
                    Subsets = subsets,
                    Metadata = new V1ObjectMeta()
                    {
                        Name = $"{namingFunction(i)}"
                    }
                };
                if (addSubset)
                {
                    for (int j = 0; j < numAddresses; j++)
                    {
                        endPoint.Subsets[0].Addresses.Add(new V1EndpointAddress
                        {
                            Hostname = addressHostNamingFunction(j)
                        });
                    }
                }
                else
                {
                    // we only want to skip addign subset in one, the rest should have subsets
                    addSubset = true;
                }

                A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetEndpointInNamespaceAsync(namingFunction(i), A<string>._, A<CancellationToken>._)).Returns(endPoint);
            }
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListServicesInNamespaceAsync(default, default, default)).WithAnyArguments().Returns(new V1ServiceList(serviceList));
        }
    }
}