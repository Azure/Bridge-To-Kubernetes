// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Autofac;
using FakeItEasy;
using FakeItEasy.Core;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.TestHelpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class KubernetesRemoteEnvironmentManagerTests : TestsBase
    {
        private KubernetesRemoteEnvironmentManager _remoteEnvironmentManager;
        private V1Pod testPod;

        internal void Setup()
        {
            var testEv = new EnvironmentVariables(new Platform());

            var testRc = RemoteContainerConnectionDetails.CreatingNewPodWithContextFromExistingService("testNameSpace", "testService", "testRoutingHeader", "testContainer");
            testPod = CreateFakePodObject(false);
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
            testRc.UpdatePodDetails(CreateFakePodObject(false));

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
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, CreateFakePodObject(false) });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsForStatefulSetAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(CreateFakePodList(true));
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsForStatefulSetAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns(CreateFakePodList(false)).Once();
        }

        private static V1PodList CreateFakePodList(Boolean? dateTimeUpdateRequired)
        {
            var podlist = new V1PodList
            {
                Items = new List<V1Pod>()
            };
            podlist.Items.Add(CreateFakePodObject(dateTimeUpdateRequired));
            return podlist;
        }

        // Verify if env vars in the pod spec match expectedEnvVars, throw exception if not.
        private static void CheckPodEnvVars(IFakeObjectCall pod, EnvironmentVariables testEv)
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

        private static V1Pod CreateFakePodObject(bool? dateTimeUpdateRequired)
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
                Name = "testContainer",
            };
            pod.Spec.Containers.Add(v1Container);
            return pod;
        }

        [Fact]
        public async void Cloned_Pod_Contains_One_Set_Of_Env_Variables()
        {
            Setup();
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
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().PatchV1StatefulSetAsync(A<string>._, A<string>._, A<V1Patch>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async void Feature_LifecycleHooks_Disabled_Should_Remove_Lifecycle()
        {
            // Setup
            Setup();

            V1Lifecycle lifecycleValue = new V1Lifecycle();
            CancellationToken cancellationToken = new CancellationToken();
            ILocalProcessConfig _localProcessConfig = A.Fake<ILocalProcessConfig>();

            // Arrange
            testPod.Spec.Containers.Single().Lifecycle = new V1Lifecycle();

            A.CallTo(() => _localProcessConfig.IsLifecycleHooksEnabled).Returns(false);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, CreateFakePodObject(false) });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) =>
            {
                V1Pod actualPod = (V1Pod)pod.Arguments[1];
                var actualContainerList = actualPod.Spec.Containers;

                foreach (var c in actualContainerList)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(c.Name, "testContainer"))
                    {
                        lifecycleValue = c.Lifecycle;
                    }
                }
            }
            ).Returns(testPod);

            // Act
            await _remoteEnvironmentManager.StartRemoteAgentAsync(_localProcessConfig, cancellationToken);

            // Assert
            Assert.Null(lifecycleValue);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async void Feature_LifecycleHooks_Enabled_Should_Not_Remove_Lifecycle()
        {
            // Setup
            Setup();

            V1Lifecycle lifecycleValue = null;
            CancellationToken cancellationToken = new CancellationToken();
            ILocalProcessConfig _localProcessConfig = A.Fake<ILocalProcessConfig>();

            // Arrange
            testPod.Spec.Containers.Single().Lifecycle = new V1Lifecycle();

            A.CallTo(() => _localProcessConfig.IsLifecycleHooksEnabled).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, CreateFakePodObject(false) });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) =>
            {
                V1Pod actualPod = (V1Pod)pod.Arguments[1];
                var actualContainerList = actualPod.Spec.Containers;

                foreach (var c in actualContainerList)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(c.Name, "testContainer"))
                    {
                        lifecycleValue = c.Lifecycle;
                    }
                }
            }
            ).Returns(testPod);

            // Act
            await _remoteEnvironmentManager.StartRemoteAgentAsync(_localProcessConfig, cancellationToken);

            // Assert
            Assert.NotNull(lifecycleValue);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async void Feature_Probes_Disabled_Should_Remove_Probes()
        {
            // Setup
            Setup();

            V1Probe livenessProbeValue = new V1Probe();
            V1Probe readinessProbeValue = new V1Probe();
            V1Probe startupProbeValue = new V1Probe();
            CancellationToken cancellationToken = new CancellationToken();
            ILocalProcessConfig _localProcessConfig = A.Fake<ILocalProcessConfig>();

            // Arrange
            testPod.Spec.Containers.Single().LivenessProbe = new V1Probe();
            testPod.Spec.Containers.Single().ReadinessProbe = new V1Probe();
            testPod.Spec.Containers.Single().StartupProbe = new V1Probe();

            A.CallTo(() => _localProcessConfig.IsProbesEnabled).Returns(false);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, CreateFakePodObject(false) });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) =>
            {
                V1Pod actualPod = (V1Pod)pod.Arguments[1];
                var actualContainerList = actualPod.Spec.Containers;

                foreach (var c in actualContainerList)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(c.Name, "testContainer"))
                    {
                        livenessProbeValue = c.LivenessProbe;
                        readinessProbeValue = c.ReadinessProbe;
                        startupProbeValue = c.StartupProbe;
                    }
                }
            }
            ).Returns(testPod);

            // Act
            await _remoteEnvironmentManager.StartRemoteAgentAsync(_localProcessConfig, cancellationToken);

            // Assert
            Assert.Null(livenessProbeValue);
            Assert.Null(readinessProbeValue);
            Assert.Null(startupProbeValue);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public async void Feature_Probes_Enabled_Should_Not_Remove_Probes()
        {
            // Setup
            Setup();

            V1Probe livenessProbeValue = null;
            V1Probe readinessProbeValue = null;
            V1Probe startupProbeValue = null;
            CancellationToken cancellationToken = new CancellationToken();
            ILocalProcessConfig _localProcessConfig = A.Fake<ILocalProcessConfig>();

            // Arrange
            testPod.Spec.Containers.Single().LivenessProbe = new V1Probe();
            testPod.Spec.Containers.Single().ReadinessProbe = new V1Probe();
            testPod.Spec.Containers.Single().StartupProbe = new V1Probe();

            A.CallTo(() => _localProcessConfig.IsProbesEnabled).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).ReturnsNextFromSequence(new[] { (V1Pod)null, CreateFakePodObject(false) });
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) =>
            {
                V1Pod actualPod = (V1Pod)pod.Arguments[1];
                var actualContainerList = actualPod.Spec.Containers;

                foreach (var c in actualContainerList)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(c.Name, "testContainer"))
                    {
                        livenessProbeValue = c.LivenessProbe;
                        readinessProbeValue = c.ReadinessProbe;
                        startupProbeValue = c.StartupProbe;
                    }
                }
            }
            ).Returns(testPod);

            // Act
            await _remoteEnvironmentManager.StartRemoteAgentAsync(_localProcessConfig, cancellationToken);

            // Assert
            Assert.NotNull(livenessProbeValue);
            Assert.NotNull(readinessProbeValue);
            Assert.NotNull(startupProbeValue);
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
    }
}