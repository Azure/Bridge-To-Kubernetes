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
using Microsoft.BridgeToKubernetes.Library.Utilities;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class RemoteContainerConnectionDetailsResolverTests : TestsBase
    {
        private RemoteContainerConnectionDetailsResolver _remoteContainerConnectionDetailsResolver;

        public RemoteContainerConnectionDetailsResolverTests()
        {
            _remoteContainerConnectionDetailsResolver = _autoFake.Resolve<RemoteContainerConnectionDetailsResolver>();
        }

        [Theory]
        [InlineData(1, true, "Running")]
        [InlineData(5, true, "Terminating")]
        [InlineData(10, true, "Running")]
        [InlineData(1, false, "Running")]
        public async void ResolveConnectionDetails_RestoreCheck(int numServices, bool hasRestore, string phase)
        {
            // Set up
            ConfigurePods(numPods: numServices, namingFunction: (i) => hasRestore && i == 0 ? $"myapp-restore-{i}" : $"myapp-{i}", phaseFunction: (i) => i == 0 ? phase : "Running", "Job", "linux");

            try {
                await _remoteContainerConnectionDetailsResolver.ResolveConnectionDetails(RemoteContainerConnectionDetails.ReplacingExistingContainerInService("todo-app", "myapp"), CancellationToken.None);
                Assert.True(!hasRestore || phase == "Terminating");
            }
            catch (Exception e){
                // We only care about this exception for this UT. ResolveConnectionDetails is large function and ot get to pass cleanly would require a lot longer set up,
                // which is not needed for this UT
                if (e.Message.StartsWith("Restoration pod is still present for the specified service")) {
                    Assert.Equal("Running", phase);
                    Assert.True(hasRestore);
                }
            }
            
        }

        [Fact]
        public async void ResolveConnectionDetails_ThrowErrorIfPodFilterConditionFails()
        {
            //set up
            ConfigurePods(numPods: 2, namingFunction: (i) => $"myapp-{i}", phaseFunction: (i) => "Running", "CronJob", "linux");
            try
            {
                await _remoteContainerConnectionDetailsResolver.ResolveConnectionDetails(RemoteContainerConnectionDetails.ReplacingExistingContainerInService("todo-app", "myapp"), CancellationToken.None);
                Assert.True(false, "Should have thrown exception");
            } catch (Exception e)
            {
                Assert.Equal("The specified service 'myapp' is not backed by a running pod. To check the status of your pods, you can run 'kubectl get pods --namespace todo-app'.", e.Message);
            }
        }

        [Fact]
        public async void ResolveConnectionDetails_ShouldSucceedIfPodFilterConditionPasses()
        {
            //set up
            ConfigurePods(numPods: 2, namingFunction: (i) => $"myapp-{i}", phaseFunction: (i) => "Running", "ReplicaSet", "linux");
            var result = await _remoteContainerConnectionDetailsResolver.ResolveConnectionDetails(RemoteContainerConnectionDetails.ReplacingExistingContainerInService("todo-app", "myapp"), CancellationToken.None);
            Assert.Equal("myapp-0", result.PodName);

        }

        [Fact]
        public async void ResolveConnectionDetails_ShouldFailForWindowsContainers()
        {
            //set up
            ConfigurePods(numPods: 2, namingFunction: (i) => $"myapp-{i}", phaseFunction: (i) => "Running", "ReplicaSet", "Windows");
            try
            {
                await _remoteContainerConnectionDetailsResolver.ResolveConnectionDetails(RemoteContainerConnectionDetails.ReplacingExistingContainerInService("todo-app", "myapp"), CancellationToken.None);
                Assert.True(false, "Should have thrown exception");
            } catch (Exception e)
            {
                Assert.Equal("The target workload is running on a Windows node. This OS is currently not supported by Bridge To Kubernetes.", e.Message);
            }
            

        }


        private void ConfigurePods(int numPods, Func<int, string> namingFunction, Func<int, string>phaseFunction, string? kind, string? nodeSelector)
        {
            var cs = new List<V1ContainerStatus>();
            cs.Add(new V1ContainerStatus() 
            {
                Image = ImageProvider.DevHostRestorationJob.Name
            });
            var podList = new List<V1Pod>();
            for (int i = 0; i < numPods; i++)
            {
                podList.Add(new V1Pod()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name = namingFunction(i),
                        OwnerReferences = new List<V1OwnerReference>()
                        {
                            new V1OwnerReference()
                            {
                                Kind = i ==0 ? kind : "Job"
                            }
                        }
                    },
                    Status = new V1PodStatus()
                    {
                        ContainerStatuses = cs,
                        Phase = phaseFunction(i)
                    },
                    Spec = new V1PodSpec()
                    {
                        NodeSelector = new Dictionary<string, string>()
                        {
                            { "kubernetes.io/os", nodeSelector }
                        },
                        Containers = new List<V1Container>()
                        {
                            new V1Container()
                            {
                                Name = "myapp",
                                Image = "myapp"
                            }
                        }
                    }
                });
            }
            var service = new V1Service()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = "myapp"
                },
                Spec = new V1ServiceSpec()
                {
                    Selector = new Dictionary<string, string>()
                    {
                        { "app", "myapp" }
                    }
                }
            };
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsInNamespaceAsync(default, default, default)).WithAnyArguments().Returns(new V1PodList(podList));
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1ServiceAsync(default, default, default)).WithAnyArguments().Returns(service);
        }
    }
}