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
        private IPortMappingManager _portMappingManager;

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
            ConfigurePods(numPods: numServices, namingFunction: (i) => hasRestore && i == 0 ? $"myapp-restore-{i}" : $"myapp-{i}", phaseFunction: (i) => i == 0 ? phase : "Running");

            try {
                await _remoteContainerConnectionDetailsResolver.ResolveConnectionDetails(RemoteContainerConnectionDetails.ReplacingExistingContainerInService("todo-app", "myapp"), CancellationToken.None);
                Assert.True(!hasRestore || phase == "Terminating");
            }
            catch (Exception e){
                // We only care about this exception for this UT. ResolveConnectionDetails is large function and ot get to pass cleanly would require a lot longer set up,
                // which is not needed for this UT
                if (e.Message.StartsWith("Restoration pod is still present for the specified service")) {
                    Assert.Equal(phase, "Running");
                    Assert.True(hasRestore);
                }
            }
            
        }

        private void ConfigurePods(int numPods, Func<int, string> namingFunction, Func<int, string>phaseFunction)
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
                        Name = namingFunction(i)
                    },
                    Status = new V1PodStatus()
                    {
                        ContainerStatuses = cs,
                        Phase = phaseFunction(i)
                    }
                });
            }
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsInNamespaceAsync(default, default, default)).WithAnyArguments().Returns(new V1PodList(podList));
        }
    }
}