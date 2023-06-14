// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using FakeItEasy.Configuration;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Restore;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob.Tests
{
    /// <summary>
    /// Tests for <see cref="RestorationJobApp"/>
    /// </summary>
    public class RestorationJobAppTests : TestsBase
    {
        private RestorationJobApp _app;
        private IRestorationJobEnvironmentVariables _env = A.Fake<IRestorationJobEnvironmentVariables>();
        private DelegatingHandler _fakeDelegatingHandler = A.Fake<DelegatingHandler>();

        public RestorationJobAppTests()
        {
            A.CallTo(() => _env.Namespace).Returns("mynamespace");
            A.CallTo(() => _env.InstanceLabelValue).Returns("foo-bar-123");
            A.CallTo(() => _env.PingInterval).Returns(TimeSpan.Zero);
            A.CallTo(() => _env.RestoreTimeout).Returns(TimeSpan.Zero);
            A.CallTo(() => _env.NumFailedPingsBeforeExit).Returns(3);
            _autoFake.Provide(_env);

            _autoFake.Provide(new HttpClient(_fakeDelegatingHandler));

            _app = _autoFake.Resolve<RestorationJobApp>();
        }

        [Fact]
        public void PatchTypeTest()
        {
            var knownPatchTypes = new List<Type>
            {
                typeof(PodDeployment),
                typeof(PodPatch),
                typeof(DeploymentPatch),
                typeof(StatefulSetPatch)
            }.OrderBy(t => t.FullName);

            var allPatchTypes = typeof(PatchEntityBase).Assembly
                .GetTypes()
                .Where(t => typeof(PatchEntityBase).IsAssignableFrom(t))
                .Where(t => t != typeof(PatchEntityBase))
                .OrderBy(t => t.FullName)
                .ToList();

            // Does RestorationJobApp contain all the necessary overloads for all patch types?
            // Look for all method calls that cast to (dynamic).
            Assert.Equal(knownPatchTypes, allPatchTypes);
        }

        [Fact]
        public void EnsureCanDeserializePatch1()
        {
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));
            var deploymentPatch = JsonHelpers.DeserializeObject<DeploymentPatch>(patchStateJson);

            string name = deploymentPatch.Deployment.Name();
            string ns = deploymentPatch.Deployment.Namespace();

            Assert.Equal("bikes", name);
            Assert.Equal("dev", ns);
        }

        [Fact]
        public void EnsureCanDeserializePatchType()
        {
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));

            var propertyName = typeof(PatchEntityBase).GetJsonPropertyName(nameof(PatchEntityBase.Type));
            string type = JsonPropertyHelpers.ParseAndGetProperty<string>(patchStateJson, propertyName);

            Assert.Equal(nameof(DeploymentPatch), type);
        }

        [Fact]
        public void EnsureFailedPingsExit()
        {
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);

            this.ConfigureHttpCall(GetFailedPingResult());

            // Verify failure to retrieve agent endpoint
            int exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));

            Assert.Equal((int)Constants.ExitCode.Fail, exitCode);
            A.CallTo(() => _autoFake.Resolve<ILog>().Error(A<string>.That.Contains("Failed to ping agent 3 times"), A<object[]>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<ILog>().Verbose(A<string>.That.Contains("Couldn't get agent endpoint"), A<object[]>._)).MustHaveHappened();
            A.CallTo(_fakeDelegatingHandler).MustNotHaveHappened();

            // Verify failure to ping agent
            Fake.ClearRecordedCalls(_autoFake.Resolve<ILog>());
            this.DeploymentPatch_Helper(true);

            exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));

            Assert.Equal((int)Constants.ExitCode.Fail, exitCode);
            A.CallTo(() => _autoFake.Resolve<ILog>().Error(A<string>.That.Contains("Failed to ping agent 3 times"), A<object[]>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<ILog>().Verbose(A<string>.That.Contains("Failed to ping agent"), A<object[]>._)).MustHaveHappened();
            A.CallTo(_fakeDelegatingHandler).MustHaveHappened(3, Times.Exactly);
        }

        [Fact]
        public void EnsureCancellation()
        {
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);
            this.DeploymentPatch_Helper(true);

            var ctx = new CancellationTokenSource();
            this.ConfigureHttpCall(GetSuccessPingResult(1))
                .Once()
                .Then
                .Invokes(() => ctx.Cancel())
                .ReturnsLazily(GetSuccessPingResult(1));

            int exitCode = _app.Execute(Array.Empty<string>(), ctx.Token);

            Assert.Equal((int)Constants.ExitCode.Cancel, exitCode);
            A.CallTo(() => _autoFake.Resolve<IRemoteRestoreJobCleaner>().CleanupRemoteRestoreJobByInstanceLabelAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
            A.CallTo(_fakeDelegatingHandler).MustHaveHappenedTwiceExactly();
        }

        [Fact]
        public void ExecutionTestWithShortWait()
        {
            A.CallTo(() => _env.PingInterval).Returns(TimeSpan.FromMilliseconds(20));
            A.CallTo(() => _env.RestoreTimeout).Returns(TimeSpan.FromMilliseconds(100));
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "PodDeployment.json"));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);
            this.PodDeployment_Helper(true);

            this.ConfigureHttpCall(GetSuccessPingResult(1))
                .Once()
                .Then
                .ReturnsLazily(GetSuccessPingResult(0));

            int exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));

            Assert.Equal(0, exitCode);
            A.CallTo(_fakeDelegatingHandler).MustHaveHappenedTwiceOrMore();
            int num = Fake.GetCalls(_fakeDelegatingHandler).Count();
            this.PodDeployment_Helper(false);
        }

        [Theory]
        [InlineData("DeploymentPatch.json", nameof(DeploymentPatch_Helper))]
        [InlineData("PodPatch.json", nameof(PodPatch_Helper))]
        [InlineData("PodDeployment.json", nameof(PodDeployment_Helper))]
        public void ExecutionTest(string patchStateFile, string testHelper)
        {
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", patchStateFile));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);

            // Setup callbacks
            var method = typeof(RestorationJobAppTests).GetMethod(testHelper, BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(this, new object[] { true });
            this.ConfigureHttpCall(GetSuccessPingResult(1))
                .NumberOfTimes(3)
                .Then
                .ReturnsLazily(GetSuccessPingResult(0));

            // Execute
            int exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));

            // Verify behavior
            Assert.Equal(0, exitCode);
            A.CallTo(_fakeDelegatingHandler).MustHaveHappened(4, Times.Exactly);
            A.CallTo(() => _autoFake.Resolve<IRemoteRestoreJobCleaner>().CleanupRemoteRestoreJobByInstanceLabelAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            method.Invoke(this, new object[] { false });
        }

        [Fact]
        public void EnsureRestoresIflastPingWithSessionsIsNullAndRestoreTimeExceeded()
        {
            // restore time is set to zero seconds while initializing this test class file.
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);
            this.DeploymentPatch_Helper(true);

            this.ConfigureHttpCall(GetSuccessPingResult(0)).NumberOfTimes(1);

            int exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));
            Assert.Equal(0, exitCode);
            A.CallTo(_fakeDelegatingHandler).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IRemoteRestoreJobCleaner>().CleanupRemoteRestoreJobByInstanceLabelAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
        }
        [Fact]
        public void EnsureNoRestoreIfRestoreTimeIsNotExceeded()
        {
            // restore time is set to 1 minute
            A.CallTo(() => _env.RestoreTimeout).Returns(TimeSpan.FromMinutes(1));
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);
            this.DeploymentPatch_Helper(true);

            this.ConfigureHttpCall(GetSuccessPingResult(0)).NumberOfTimes(1);

            int exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));
            Assert.Equal(1, exitCode);
            A.CallTo(_fakeDelegatingHandler).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _autoFake.Resolve<IRemoteRestoreJobCleaner>().CleanupRemoteRestoreJobByInstanceLabelAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        [Fact]
        public void EnsureNoRestoreIflastPingWithSessionsIsNotNull()
        {
            string patchStateJson = File.ReadAllText(Path.Combine("TestData", "DeploymentPatch.json"));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(DevHostConstants.DevHostRestorationJob.PatchStateFullPath, A<int>._)).Returns(patchStateJson);
            this.DeploymentPatch_Helper(true);

            this.ConfigureHttpCall(GetSuccessPingResult(3))
                .NumberOfTimes(1);

            int exitCode = _app.Execute(Array.Empty<string>(), default(CancellationToken));
            Assert.Equal(1, exitCode);
            A.CallTo(_fakeDelegatingHandler).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _autoFake.Resolve<IRemoteRestoreJobCleaner>().CleanupRemoteRestoreJobByInstanceLabelAsync(A<string>._, A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
        }

        #region Test helpers

        private void DeploymentPatch_Helper(bool isSetup)
        {
            if (isSetup)
            {
                A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().ListPodsForDeploymentAsync(A<string>._, A<string>._, A<CancellationToken>._))
                    .Returns(new V1PodList
                    {
                        Items = new V1Pod[] { _CreateDevHostPod() }
                    });
            }
            else
            {
                // Verify
                A.CallTo(() => _autoFake.Resolve<IWorkloadRestorationService>().RestoreDeploymentPatchAsync(A<DeploymentPatch>._, A<CancellationToken>._, A<Action<ProgressMessage>>._, A<bool>._))
                    .MustHaveHappenedOnceExactly();
            }
        }

        private void PodPatch_Helper(bool isSetup)
        {
            if (isSetup)
            {
                A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._))
                    .Returns(_CreateDevHostPod());
            }
            else
            {
                // Verify
                A.CallTo(() => _autoFake.Resolve<IWorkloadRestorationService>().RestorePodPatchAsync(A<PodPatch>._, A<CancellationToken>._, A<Action<ProgressMessage>>._, A<bool>._))
                    .MustHaveHappenedOnceExactly();
            }
        }

        private void PodDeployment_Helper(bool isSetup)
        {
            if (isSetup)
            {
                A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._))
                     .Returns(_CreateDevHostPod());
            }
            else
            {
                // Verify
                A.CallTo(() => _autoFake.Resolve<IWorkloadRestorationService>().RemovePodDeploymentAsync(A<PodDeployment>._, A<CancellationToken>._, A<Action<ProgressMessage>>._, A<bool>._))
                    .MustHaveHappenedOnceExactly();
            }
        }

        #endregion Test helpers

        private V1Pod _CreateDevHostPod()
        {
            var pod = new V1Pod
            {
                Metadata = new V1ObjectMeta(namespaceProperty: "mynamespace", name: "mypod"),
                Spec = new V1PodSpec
                {
                    Containers = new V1Container[]
                    {
                        new V1Container
                        {
                            Image = "mindaro/devhostagent:1234"
                        }
                    }
                },
                Status = new V1PodStatus
                {
                    PodIP = "1.2.3.4"
                }
            };

            return pod;
        }

        private IAfterCallConfiguredWithOutAndRefParametersConfiguration<IReturnValueConfiguration<Task<HttpResponseMessage>>> ConfigureHttpCall(Func<HttpResponseMessage> response)
        {
            return A.CallTo(_fakeDelegatingHandler)
                .Where(x => x.Method.Name == "SendAsync")
                .WithReturnType<Task<HttpResponseMessage>>()
                .ReturnsLazily(response);
        }

        private Func<HttpResponseMessage> GetSuccessPingResult(int numSessions)
        {
            return () => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(@$"{{ ""numConnectedSessions"": {numSessions} }}")
            };
        }

        private Func<HttpResponseMessage> GetFailedPingResult()
        {
            return () => new HttpResponseMessage(HttpStatusCode.InternalServerError);
        }
    }
}