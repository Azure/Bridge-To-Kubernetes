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
using Microsoft.BridgeToKubernetes.Common.Json;
using Xunit;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Common.IO;
using System.Threading.Tasks;
using FakeItEasy.Core;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.Models.Kubernetes;
using SystemTextJsonPatch;
using static k8s.Models.V1Patch;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class KubernetesRemoteEnvironmentManagerTests : TestsBase
    {
        private KubernetesRemoteEnvironmentManager _remoteEnvironmentManager;
        private V1Pod testPod;


        internal void SetUp()
        {
            var testEv = new EnvironmentVariables(new Platform());

            var testRc = RemoteContainerConnectionDetails.CreatingNewPodWithContextFromExistingService("testNameSpace", "testService", "testRoutingHeader", "testContainer");
            testPod = createFakePodObject(false);
            testRc.UpdatePodDetails(testPod);

            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(() => Task.FromResult(testRc));
            var environmentVariables = new AsyncLazy<IEnvironmentVariables>(() => testEv);
            _remoteEnvironmentManager = _autoFake.Resolve<KubernetesRemoteEnvironmentManager>(TypedParameter.From(remoteContainerConnectionDetails), TypedParameter.From(environmentVariables));
        }

        internal void SetUpForStateful()
        {
            var testEv = new EnvironmentVariables(new Platform());

            var testRc = RemoteContainerConnectionDetails.ReplacingExistingContainerInDeployment("testNameSpace", "testService", "testRoutingHeader", "testContainer");
            V1StatefulSet v1StatefulSet;
            V1Container container;
            CreateFakeStateFulSet(out v1StatefulSet, out container);
            testRc.UpdateStatefulSetDetails(v1StatefulSet);
            testRc.UpdateSourceEntityTypeToStatefulSet(testRc.StatefulSet);
            testRc.UpdateContainerDetails(container);
            testRc.UpdatePodDetails(createFakePodObject(false));

            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(() => Task.FromResult(testRc));
            var environmentVariables = new AsyncLazy<IEnvironmentVariables>(() => testEv);
            _remoteEnvironmentManager = _autoFake.Resolve<KubernetesRemoteEnvironmentManager>(TypedParameter.From(remoteContainerConnectionDetails), TypedParameter.From(environmentVariables));

        }

        private static void CreateFakeStateFulSet(out V1StatefulSet v1StatefulSet, out V1Container container)
        {
            v1StatefulSet = new()
            {
                Metadata = new V1ObjectMeta()
            };
            v1StatefulSet.Metadata.Name = "fakeStateful";
            v1StatefulSet.Spec = new V1StatefulSetSpec
            {
                Template = new V1PodTemplateSpec()
            };
            v1StatefulSet.Spec.Template.Spec = new V1PodSpec
            {
                Containers = new List<V1Container>()
            };
            container = new V1Container
            {
                Name = "testContainer"
            };
            v1StatefulSet.Spec.Template.Spec.Containers.Add(container);
        }

        private void SetupTestMocks()
        {
            var testEv = new EnvironmentVariables(new Platform());
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) => CheckPodEnvVars(pod, testEv)).Returns(testPod);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, createFakePodObject(false) });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsForStatefulSetAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(createFakePodList(true));
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsForStatefulSetAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(createFakePodList(false)).Once();
        }

        private V1PodList createFakePodList(Boolean? dateTimeUpdateRequired)
        {
            var podlist = new V1PodList
            {
                Items = new List<V1Pod>()
            };
            podlist.Items.Add(createFakePodObject(dateTimeUpdateRequired));
            return podlist;
        }

        // Verify if env vars in the pod spec match expectedEnvVars, throw exception if not.
        private void CheckPodEnvVars(IFakeObjectCall pod, EnvironmentVariables testEv)
        {
            V1Pod actualPod = (V1Pod)pod.Arguments[1];
            var actualContainerList = (IList<V1Container>)actualPod.Spec.Containers;
            var actualEnvList = actualContainerList.First(c => c.Env != null).Env;
            List<string> expectedEnvVars = new List<string> { EnvironmentVariables.Names.CollectTelemetry, EnvironmentVariables.Names.ConsoleVerbosity, EnvironmentVariables.Names.CorrelationId };

            Assert.Equal(expectedEnvVars.Count, actualEnvList.Count);
            Dictionary<string, string> podEnvVars = new Dictionary<string, string>();
            foreach (var envVar in actualEnvList)
            {
                Assert.Contains(envVar.Name, expectedEnvVars);
                podEnvVars.Add(envVar.Name, envVar.Value);
            }

            Assert.Equal(testEv.CollectTelemetry.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.CollectTelemetry));
            Assert.Equal(LoggingVerbosity.Verbose.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.ConsoleVerbosity));
            Assert.Equal("", podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.CorrelationId));

        }

        public V1Pod createFakePodObject(Boolean? dateTimeUpdateRequired)
        {
            V1Pod pod = new()
            {
                Status = new V1PodStatus()
            };
            pod.Status.ContainerStatuses = new List<V1ContainerStatus>();
            pod.Status.Phase = "running";
            pod.Metadata = new V1ObjectMeta
            {
                Name = "testName",
                NamespaceProperty = "testname",
                CreationTimestamp = dateTimeUpdateRequired == true ? DateTime.Now.AddHours(1) : DateTime.Now
            };
            pod.Spec = new V1PodSpec
            {
                Containers = new List<V1Container>()
            };
            V1Container v1Container = new()
            {
                Name = "testContainer"
            };
            pod.Spec.Containers.Add(v1Container);
            return pod;
        }


        [Fact]
        public async void Cloned_Pod_Contains_One_Set_Of_Env_Variables()
        { 
            SetUp();
            ILocalProcessConfig localProcessConfig = null;
            CancellationToken cancellationToken = new CancellationToken();
            
            SetupTestMocks();

            await _remoteEnvironmentManager.StartRemoteAgentAsync(localProcessConfig, cancellationToken);
        }

        [Fact]
        public async void Stateful_Clone_Should_Work()
        {
            SetUpForStateful();
            ILocalProcessConfig localProcessConfig = null;
            CancellationToken cancellationToken = new CancellationToken();

            SetupTestMocks();

            await _remoteEnvironmentManager.StartRemoteAgentAsync(localProcessConfig, cancellationToken);
           
            // todo - it would be better to have actual values instead of any values.
            // var patch = new JsonPatchDocument<V1StatefulSet>();
            // var expectedPatchJson = new V1Patch(JsonHelpers.SerializeObject(patch), PatchType.JsonPatch);
            // A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().PatchV1StatefulSetAsync("testNameSpace", "fakeStateful", expectedPatchJson , cancellationToken)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().PatchV1StatefulSetAsync(A<string>._, A<string>._,A<V1Patch>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
        
        
    }
}