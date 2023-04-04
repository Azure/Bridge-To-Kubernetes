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
        [InlineData(1, 100, true)]
        [InlineData(5, 20, false)]
        [InlineData(10, 3, true)]
        [InlineData(2, 3, false)]
        public async void GetReachableServicesAsync_HeadlessService(int numServices, int numAddresses, bool isInWorkloadNamespace)
        {
            List<string> expectedDnsList = ConfigureHeadlessService(numServices: numServices, namingFunction: (i) => $"myapp-{i}", numAddresses: numAddresses, addressHostNamingFunction: (i) => $"Host-{i}", isInWorkloadNamespace);
            var resultRechableEndpoints = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: isInWorkloadNamespace ? "testNamespace" : "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));
            // Doing numServices-1 when calculating because we are adding empty subset for one service and that will be skipped
            Assert.Equal((numServices-1) * (numAddresses), resultRechableEndpoints.Count());
            foreach (var endpoint in resultRechableEndpoints) {
                if (endpoint.Ports.Any()) {
                    Assert.Equal(endpoint.Ports.ElementAt(0).LocalPort, -1);
                    bool found = false;
                    foreach (var dns in expectedDnsList) {
                        if (string.Equals(endpoint.DnsName, dns))
                        {
                            found = true;
                            expectedDnsList.Remove(dns);
                            break;
                        }
                    }
                    Assert.True(found);
                }
            }  
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_PortsToIgnore_HeadlessService(int numAddresses)
        { 
            // Create two services and specify their ports to ignore and ports to use
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", "190, 300");
            ignorePorts.Add("ServiceB", "23");

            Dictionary<string, List<int>> servicePorts = new Dictionary<string, List<int>>();
            servicePorts.Add("ServiceA", new List<int>{234, 421, 300, 121});
            servicePorts.Add("ServiceB", new List<int>{89, 23, 111, 324});

            ConfigureServiceWithIgnorePorts(isHeadLessService: true, numAddresses, ignorePorts, servicePorts);
            // Generate service endpoints
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));                       
            
            // Find all ports which were allocated to each service
            Dictionary<string, List<int>> portsAllocatedToService = new Dictionary<string, List<int>>();
            foreach (var endpoint in result) 
            {
                var ports = portsAllocatedToService.GetOrAdd(endpoint.DnsName, () => new List<int>());
                ports.AddRange(endpoint.Ports.Select( p => p.RemotePort));
            }
            
            // For each service ensure that none of the ignore ports are in the ports allocated and all other servicePorts are allocated to the service
            // Check if all services are in the result
            Assert.True(CompareLists(ignorePorts.Keys.Select( s => $"host-0.{s}.").ToList(), portsAllocatedToService.Keys.ToList()));
            foreach(var serviceName in ignorePorts.Keys) 
            {
                // Identify expected ports for a service.
                var ignorePortsList = ignorePorts.GetValueOrDefault(serviceName)?.Split(",").Select(p => int.Parse(p)).ToList() ?? new List<int>();
                var expectedPortsForService = servicePorts.GetValueOrDefault(serviceName)?.Where( p => !ignorePortsList.Contains(p)) ?? new List<int>();

                // Check if the allocated ports (from result) are same as the expected ports for the service.
                Assert.True(CompareLists(expectedPortsForService.ToList(), portsAllocatedToService.GetValueOrDefault($"host-0.{serviceName}.")));
            }
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_PortsToIgnore_Service(int numAddresses)
        { 
            // Create two services and specify their ports to ignore and ports to use
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", "190, 300");
            ignorePorts.Add("ServiceB", "23");

            Dictionary<string, List<int>> servicePorts = new Dictionary<string, List<int>>();
            servicePorts.Add("ServiceA", new List<int>{234, 421, 300, 121});
            servicePorts.Add("ServiceB", new List<int>{89, 23, 111, 324});

            ConfigureServiceWithIgnorePorts(isHeadLessService: false, numAddresses, ignorePorts, servicePorts, "NodePort");
            // Generate service endpoints
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));                       
            
            // Find all ports which were allocated to each service
            Dictionary<string, List<int>> portsAllocatedToService = new Dictionary<string, List<int>>();
            foreach (var endpoint in result) 
            {
                var ports = portsAllocatedToService.GetOrAdd(endpoint.DnsName, () => new List<int>());
                ports.AddRange(endpoint.Ports.Select( p => p.RemotePort));
            }
            
            // For each service ensure that none of the ignore ports are in the ports allocated and all other servicePorts are allocated to the service
            // Check if all services are in the result
            Assert.True(CompareLists(ignorePorts.Keys.Select( s => $"{s}.").ToList(), portsAllocatedToService.Keys.ToList()));
            foreach(var serviceName in ignorePorts.Keys) 
            {
                // Identify expected ports for a service.
                var ignorePortsList = ignorePorts.GetValueOrDefault(serviceName)?.Split(",").Select(p => int.Parse(p)).ToList() ?? new List<int>();
                var expectedPortsForService = servicePorts.GetValueOrDefault(serviceName)?.Where( p => !ignorePortsList.Contains(p)) ?? new List<int>();

                // Check if the allocated ports (from result) are same as the expected ports for the service.
                Assert.True(CompareLists(expectedPortsForService.ToList(), portsAllocatedToService.GetValueOrDefault($"{serviceName}.")));
            }
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_NoPortsToIgnore_HeadlessService(int numAddresses) 
        {
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", null);
            ignorePorts.Add("ServiceB", null);

            Dictionary<string, List<int>> servicePorts = new Dictionary<string, List<int>>();
            servicePorts.Add("ServiceA", new List<int>{234, 421, 300, 121});
            servicePorts.Add("ServiceB", new List<int>{89, 23, 111, 324});

            ConfigureServiceWithIgnorePorts(isHeadLessService: true, numAddresses, ignorePorts, servicePorts);
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));
            
            // Find all ports which were allocated to each service
            Dictionary<string, List<int>> portsAllocatedToService = new Dictionary<string, List<int>>();
            foreach (var endpointInfo in result) 
            {
                var ports = portsAllocatedToService.GetOrAdd(endpointInfo.DnsName, () => new List<int>());
                ports.AddRange(endpointInfo.Ports.Select( p => p.RemotePort));               
            }  
            
            // For each service ensure that none of the ignore ports are in the ports allocated and all other servicePorts are allocated to the service
            Assert.True(CompareLists(ignorePorts.Keys.Select( s => $"host-0.{s}.").ToList(), portsAllocatedToService.Keys.ToList()));
            
            foreach(var serviceName in ignorePorts.Keys) 
            {
                // Identify expected ports for a service.
                var ignorePortsList = ignorePorts.GetValueOrDefault(serviceName)?.Split(",").Select(p => int.Parse(p)).ToList() ?? new List<int>();
                var expectedPortsForService = servicePorts.GetValueOrDefault(serviceName)?.Where( p => !ignorePortsList.Contains(p)) ?? new List<int>();

                // Check if the allocated ports (from result) are same as the expected ports for the service.
                Assert.True(CompareLists(expectedPortsForService.ToList(), portsAllocatedToService.GetValueOrDefault($"host-0.{serviceName}.")));
            }        
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_NoPortsToIgnore_Service(int numAddresses) 
        {
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", null);
            ignorePorts.Add("ServiceB", null);

            Dictionary<string, List<int>> servicePorts = new Dictionary<string, List<int>>();
            servicePorts.Add("ServiceA", new List<int>{234, 421, 300, 121});
            servicePorts.Add("ServiceB", new List<int>{89, 23, 111, 324});

            ConfigureServiceWithIgnorePorts(isHeadLessService: false, numAddresses, ignorePorts, servicePorts);
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));
            
            // Find all ports which were allocated to each service
            Dictionary<string, List<int>> portsAllocatedToService = new Dictionary<string, List<int>>();
            foreach (var endpointInfo in result) 
            {
                var ports = portsAllocatedToService.GetOrAdd(endpointInfo.DnsName, () => new List<int>());
                ports.AddRange(endpointInfo.Ports.Select( p => p.RemotePort));               
            }  
            
            // For each service ensure that none of the ignore ports are in the ports allocated and all other servicePorts are allocated to the service
            Assert.True(CompareLists(ignorePorts.Keys.Select( s => $"{s}.").ToList(), portsAllocatedToService.Keys.ToList()));
            
            foreach(var serviceName in ignorePorts.Keys) 
            {
                // Identify expected ports for a service.
                var ignorePortsList = ignorePorts.GetValueOrDefault(serviceName)?.Split(",").Select(p => int.Parse(p)).ToList() ?? new List<int>();
                var expectedPortsForService = servicePorts.GetValueOrDefault(serviceName)?.Where( p => !ignorePortsList.Contains(p)) ?? new List<int>();

                // Check if the allocated ports (from result) are same as the expected ports for the service.
                Assert.True(CompareLists(expectedPortsForService.ToList(), portsAllocatedToService.GetValueOrDefault($"{serviceName}.")));
            }        
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_NoPortsToIgnore_NodePortService(int numAddresses) 
        {
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", null);
            ignorePorts.Add("ServiceB", null);

            Dictionary<string, List<int>> servicePorts = new Dictionary<string, List<int>>();
            servicePorts.Add("ServiceA", new List<int>{234, 421, 300, 121});
            servicePorts.Add("ServiceB", new List<int>{89, 23, 111, 324});

            ConfigureServiceWithIgnorePorts(isHeadLessService: false, numAddresses, ignorePorts, servicePorts, "NodePort");
            var result = await _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken));
            
            // Find all ports which were allocated to each service
            Dictionary<string, List<int>> portsAllocatedToService = new Dictionary<string, List<int>>();
            foreach (var endpointInfo in result) 
            {
                var ports = portsAllocatedToService.GetOrAdd(endpointInfo.DnsName, () => new List<int>());
                ports.AddRange(endpointInfo.Ports.Select( p => p.RemotePort));               
            }  
            
            // For each service ensure that none of the ignore ports are in the ports allocated and all other servicePorts are allocated to the service
            Assert.True(CompareLists(ignorePorts.Keys.Select( s => $"{s}.").ToList(), portsAllocatedToService.Keys.ToList()));
            
            foreach(var serviceName in ignorePorts.Keys) 
            {
                // Identify expected ports for a service.
                var ignorePortsList = ignorePorts.GetValueOrDefault(serviceName)?.Split(",").Select(p => int.Parse(p)).ToList() ?? new List<int>();
                var expectedPortsForService = servicePorts.GetValueOrDefault(serviceName)?.Where( p => !ignorePortsList.Contains(p)) ?? new List<int>();

                // Check if the allocated ports (from result) are same as the expected ports for the service.
                Assert.True(CompareLists(expectedPortsForService.ToList(), portsAllocatedToService.GetValueOrDefault($"{serviceName}.")));
            }        
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_PortsToIgnoreIncorrectFormat_HeadlessService(int numAddresses) {
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", "19o, abc");
            ConfigureServiceWithIgnorePorts(isHeadLessService: true, numAddresses, ignorePorts);
            await Assert.ThrowsAsync<UserVisibleException>(() => _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken)));
        }

        [Theory]
        [InlineData(1)]
        public async void GetReachableServicesAsync_PortsToIgnoreIncorrectFormat_Service(int numAddresses) {
            Dictionary<string, string> ignorePorts = new Dictionary<string, string>();
            ignorePorts.Add("ServiceA", "19o, abc");
            ConfigureServiceWithIgnorePorts(isHeadLessService: false, numAddresses, ignorePorts);
            await Assert.ThrowsAsync<UserVisibleException>(() => _workloadInformationProvider.GetReachableEndpointsAsync(namespaceName: "", localProcessConfig: null, includeSameNamespaceServices: true, cancellationToken: default(CancellationToken)));
        }

        // Comparator when the list elements are unique.
        private static bool CompareLists<T>(List<T> list1, List<T> list2) 
        {
            if (list1.Count != list2.Count)
            {
                return false;
            }
            return list1.All( item => list2.Contains(item));
        }

        private void ConfigureServiceWithIgnorePorts(bool isHeadLessService, int numAddresses, Dictionary<string, string> ignorePorts, Dictionary<string, List<int>> servicePorts = null, string serviceType = "ClusterIP") 
        {
            var serviceList = new List<V1Service>();
            // ignorePorts.Keys has all services used in the test.
            foreach (var serviceName in ignorePorts.Keys) 
            {
                var service = new V1Service() 
                {
                    Spec = new V1ServiceSpec() 
                    {
                        Type = serviceType,
                        ClusterIP = isHeadLessService ? "None" : "10.1.1.1", 
                        Ports = servicePorts?.GetValueOrDefault(serviceName).Select(p => new V1ServicePort(port: p, protocol: "TCP")).ToList() ?? new List<V1ServicePort> { new V1ServicePort(port: 80, protocol: "TCP")}, 
                        Selector = new Dictionary<string, string> { { "app", "myapp" } }
                    },
                    Metadata = new V1ObjectMeta()
                    {
                        Name = serviceName
                    }
                };
                if (ignorePorts.GetValueOrDefault(serviceName) != null) {
                    service.Metadata.Annotations = new Dictionary<string, string>();
                    service.Metadata.Annotations.Add(Constants.DeploymentConfig.ServiceAnnotations, ignorePorts.GetValueOrDefault(serviceName));
                }               
                serviceList.Add(service);
                var subsets = new List<V1EndpointSubset>()
                        {
                            new V1EndpointSubset()
                            {
                                Ports = servicePorts?.GetValueOrDefault(serviceName).Select(p => new Corev1EndpointPort(port: p, protocol: "TCP")).ToList() ?? new List<Corev1EndpointPort> { new Corev1EndpointPort(port: 80, protocol: "TCP") },
                                Addresses =  new List<V1EndpointAddress>()
                            }
                        };
                
                var endPoint = new V1Endpoints()
                {
                    Subsets = subsets,
                    Metadata = new V1ObjectMeta()
                    {
                        Name = serviceName
                    }
                };
                for (int j = 0; j < numAddresses; j++)
                    {
                        endPoint.Subsets[0].Addresses.Add(new V1EndpointAddress
                        {
                            Hostname = $"host-{j}",
                        });
                    }
                A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetEndpointInNamespaceAsync(serviceName, A<string>._, A<CancellationToken>._)).Returns(endPoint);
            }
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListServicesInNamespaceAsync(default, default, default)).WithAnyArguments().Returns(new V1ServiceList(serviceList));
        }

        private List<string> ConfigureHeadlessService(int numServices, Func<int, string> namingFunction, int numAddresses, Func<int, string> addressHostNamingFunction, bool isInWorkloadNamespace)
        {
            var result = new List<string>();
            var serviceList = new List<V1Service>();
            // introducing this variable so we can add an endpoint with empty subset to have crash coverage
            bool addSubset = false;
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
                        Name = namingFunction(i),
                        NamespaceProperty = "testNamespace"
                    }
                });
                var subsets = new List<V1EndpointSubset>()
                        {
                            new V1EndpointSubset()
                            {
                                Ports = new List<Corev1EndpointPort> { new Corev1EndpointPort(port: 80, protocol: "TCP") },
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
                        Name = $"{namingFunction(i)}",
                        NamespaceProperty = "testNamespace"
                    }
                };
                if (addSubset)
                {
                    for (int j = 0; j < numAddresses; j++)
                    {
                        // Allow empty hostname for better coverage
                        string hostname = j%2 == 0 ? addressHostNamingFunction(j) : "";
                        endPoint.Subsets[0].Addresses.Add(new V1EndpointAddress
                        {
                            Hostname = hostname
                        });
                        if (!string.IsNullOrEmpty(hostname))
                        {
                            result.Add(isInWorkloadNamespace ?
                                    $"{hostname}.{namingFunction(i)}" :
                                    $"{hostname}.{namingFunction(i)}.testNamespace");
                        }
                        else
                        {
                            result.Add(isInWorkloadNamespace ? 
                                    namingFunction(i) : 
                                    $"{namingFunction(i)}.testNamespace");
                        }
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
            return result;
        }
    }
}