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

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class KubernetesRemoteEnvironmentManagerTests : TestsBase
    {
        private IRemoteEnvironmentManager _remoteEnvironmentManager;

        public KubernetesRemoteEnvironmentManagerTests() 
        {
            var testEv = new EnvironmentVariables(new Platform());

            var testRc = RemoteContainerConnectionDetails.CreatingNewPodWithContextFromExistingService("testNameSpace", "testService", "testRoutingHeader");
            var testPod = new V1Pod();
            testPod.Metadata = new V1ObjectMeta();
            testPod.Metadata.Name = "testName";
            testRc.UpdatePodDetails(testPod);
            
            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(() => Task.FromResult(testRc));
            var environmentVariables = new AsyncLazy<IEnvironmentVariables>(() => testEv);
            _remoteEnvironmentManager = _autoFake.Resolve<IRemoteEnvironmentManager>(TypedParameter.From(remoteContainerConnectionDetails), TypedParameter.From(environmentVariables));
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
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().CreateV1PodAsync(A<string>._, A<V1Pod>._, A<CancellationToken>._)).Invokes((pod) => CheckPodEnvVars((V1Pod)pod, testEv));
            A.CallTo(() => _autoFake.Resolve<IKubernetesClient>().GetV1PodAsync(A<string>._, A<string>._, A<CancellationToken>._)).Returns((V1Pod)null);
        }

        // Verify if env vars in the pod spec match expectedEnvVars, throw exception if not.
        private void CheckPodEnvVars(V1Pod pod, EnvironmentVariables testEv)
        {        
            List<string> expectedEnvVars = new List<string> {EnvironmentVariables.Names.CollectTelemetry, EnvironmentVariables.Names.ConsoleVerbosity, EnvironmentVariables.Names.CorrelationId};

            foreach(var container in pod.Spec.Containers) 
            {
                Assert.Equal(expectedEnvVars.Count, container.Env.Count);
                Dictionary<string, string> podEnvVars = new Dictionary<string, string>();
                foreach(var envVar in container.Env)
                {
                    Assert.True(expectedEnvVars.Contains(envVar.Name));
                    podEnvVars.Add(envVar.Name, envVar.Value);
                }

                Assert.Equal(testEv.CollectTelemetry.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.CollectTelemetry));
                Assert.Equal(LoggingVerbosity.Verbose.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.ConsoleVerbosity));
                Assert.Equal(testEv.CorrelationId.ToString(), podEnvVars.GetValueOrDefault(EnvironmentVariables.Names.CorrelationId));
            }
        }
    }
}