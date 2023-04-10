// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac;
using System.Threading;
using k8s.Models;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Xunit;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Common.IO;
using System.Threading.Tasks;
using FakeItEasy.Core;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.Models.Kubernetes;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class KubernetesRemoteEnvironmentManagerTests : TestsBase
    {
        private KubernetesRemoteEnvironmentManager _remoteEnvironmentManager;
        private V1Pod testPod;

        public KubernetesRemoteEnvironmentManagerTests() 
        {
            var testEv = new EnvironmentVariables(new Platform());

            var testRc = RemoteContainerConnectionDetails.CreatingNewPodWithContextFromExistingService("testNameSpace", "testService", "testRoutingHeader", "testContainer");
            testPod = createFakePodObject();
            testRc.UpdatePodDetails(testPod);
            
            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(() => Task.FromResult(testRc));
            var environmentVariables = new AsyncLazy<IEnvironmentVariables>(() => testEv);
            _remoteEnvironmentManager = _autoFake.Resolve<KubernetesRemoteEnvironmentManager>(TypedParameter.From(remoteContainerConnectionDetails), TypedParameter.From(environmentVariables));
        }

        [Fact]
        public async void Cloned_Pod_Contains_One_Set_Of_Env_Variables()
        { 
            ILocalProcessConfig localProcessConfig = null;
            CancellationToken cancellationToken = new CancellationToken();
            
            SetupTestMocks();

            await _remoteEnvironmentManager.StartRemoteAgentAsync(localProcessConfig, cancellationToken);
        }
        
        private void SetupTestMocks()
        {
            var testEv = new EnvironmentVariables(new Platform());
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) => CheckPodEnvVars(pod, testEv)).Returns(testPod);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, createFakePodObject() });
        }

        // Verify if env vars in the pod spec match expectedEnvVars, throw exception if not.
        private void CheckPodEnvVars(IFakeObjectCall pod, EnvironmentVariables testEv)
        {
            V1Pod actualPod = (V1Pod) pod.Arguments[1];
            var actualContainerList = (IList<V1Container>) actualPod.Spec.Containers;
            var actualEnvList = actualContainerList.First(c => c.Env != null).Env;
            List<string> expectedEnvVars = new List<string> {EnvironmentVariables.Names.CollectTelemetry, EnvironmentVariables.Names.ConsoleVerbosity, EnvironmentVariables.Names.CorrelationId};

                Assert.Equal(expectedEnvVars.Count, actualEnvList.Count);
                Dictionary<string, string> podEnvVars = new Dictionary<string, string>();
                foreach(var envVar in actualEnvList)
                {
                    Assert.Contains(envVar.Name, expectedEnvVars);
                    podEnvVars.Add(envVar.Name, envVar.Value);
                }

                Assert.Equal(testEv.CollectTelemetry.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.CollectTelemetry));
                Assert.Equal(LoggingVerbosity.Verbose.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.ConsoleVerbosity));
                Assert.Equal("", podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.CorrelationId));
            
        }

        public V1Pod createFakePodObject()
        {
            V1Pod pod = new V1Pod();
            pod.Status = new V1PodStatus();
            pod.Status.ContainerStatuses = new List<V1ContainerStatus>();
            pod.Status.Phase = "running";
            pod.Metadata = new V1ObjectMeta();
            pod.Metadata.Name = "testName";
            pod.Metadata.NamespaceProperty = "testname";
            pod.Spec = new V1PodSpec();
            pod.Spec.Containers = new List<V1Container>();
            V1Container v1Container = new V1Container();
            v1Container.Name = "testContainer";
            pod.Spec.Containers.Add(v1Container);
            return pod;
        }
    }
}