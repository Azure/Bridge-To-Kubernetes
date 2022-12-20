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
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class PortMappingManagerTest : TestsBase
    {
        private IWorkloadInformationProvider _workloadInformationProvider;
        private IPortMappingManager _portMappingManager;

        public PortMappingManagerTest()
        {
            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(async () => _autoFake.Resolve<RemoteContainerConnectionDetails>());
            _workloadInformationProvider = _autoFake.Resolve<WorkloadInformationProvider>(TypedParameter.From(remoteContainerConnectionDetails));
            A.CallTo(() => _autoFake.Resolve<IPlatform>().IsOSX).Returns(true);
            _portMappingManager = _autoFake.Resolve<PortMappingManager>();
        }

        [Theory]
        [InlineData(1, 100)]
        [InlineData(5, 20)]
        [InlineData(10, 3)]
        [InlineData(1, 3)]
        public async void GetRemoteToFreeLocalPortMappings_HeadlessService(int numServices, int numAddresses)
        {
            // Set up
            ConfigureHeadlessService(numServices: numServices, namingFunction: (i) => $"myapp-{i}", numAddresses: numAddresses, addressHostNamingFunction: (i) => $"Host-{i}");
            var endpoints = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));
            
            // Method to be tested
            endpoints = _portMappingManager.GetRemoteToFreeLocalPortMappings(endpoints);

            // Verification
            Assert.Equal(numServices * (numAddresses), endpoints.Count());
            var assignedPorts = new HashSet<int>();
            foreach (var endpoint in endpoints) {
                foreach (var port in endpoint.Ports) {
                    Assert.NotEqual(port.LocalPort, -1);
                    Assert.False(assignedPorts.Contains(port.LocalPort));
                    assignedPorts.Add(port.LocalPort);
                }
            }   
        }

        private void ConfigureHeadlessService(int numServices, Func<int, string> namingFunction, int numAddresses, Func<int, string> addressHostNamingFunction)
        {
            var serviceList = new List<V1Service>();
            for (int i = 0; i < numServices; i++)
            {
                serviceList.Add(new V1Service()
                {
                    Spec = new V1ServiceSpec()
                    {
                        Type = "ClusterIP",
                        ClusterIP = "None",
                        Ports = new List<V1ServicePort> { new V1ServicePort(port: 80, protocol: "TCP") },
                        Selector = new Dictionary<string, string> { { "app", "myapp" } }
                    },
                    Metadata = new V1ObjectMeta()
                    {
                        Name = namingFunction(i)
                    }
                });
                var endPoint = new V1Endpoints()
                {
                    Subsets = new List<V1EndpointSubset>()
                        {
                            new V1EndpointSubset()
                            {
                                Ports = new List<Corev1EndpointPort> { new Corev1EndpointPort(port: 80, protocol: "TCP") },
                                Addresses =  new List<V1EndpointAddress>()
                            }
                        },
                    Metadata = new V1ObjectMeta()
                    {
                        Name = $"{namingFunction(i)}"
                    }
                };
                for (int j = 0; j < numAddresses; j++)
                {
                    endPoint.Subsets[0].Addresses.Add(new V1EndpointAddress
                    {
                        Hostname = addressHostNamingFunction(j)
                    });
                }
                A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetEndpointInNamespaceAsync(namingFunction(i), A<string>._, A<CancellationToken>._)).Returns(endPoint);
            }
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListServicesInNamespaceAsync(default, default, default)).WithAnyArguments().Returns(new V1ServiceList(serviceList));
        }
    }
}