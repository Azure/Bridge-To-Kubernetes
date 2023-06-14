// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using FakeItEasy;
using k8s;
using k8s.KubeConfigModels;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.TestHelpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Kubernetes
{
    public class K8sClientFactoryTests : TestsBase
    {
        [Fact]
        public void CreateFromInClusterConfig()
        {
            const string kubernetesInClusterConfigOverwrite = "X:\\fake";
            const string tokenFile = $"{kubernetesInClusterConfigOverwrite}\\token";
            const string caFile = $"{kubernetesInClusterConfigOverwrite}\\ca.crt";
            const string host = "api.fake.com";
            const string port = "443";

            string caValue = File.ReadAllText(Path.Combine("TestData", "ca.txt"));
            string tokenValue = File.ReadAllText(Path.Combine("TestData", "token.txt"));

            var fakeFileSystem = A.Fake<IFileSystem>();
            A.CallTo(() => fakeFileSystem.ReadAllTextFromFile(tokenFile, 1)).Returns(tokenValue);

            var ms = new MemoryStream(Encoding.UTF8.GetBytes(caValue));
            A.CallTo(() => fakeFileSystem.OpenFileForRead(caFile)).Returns(ms);

            var fakeEnv = A.Fake<IEnvironmentVariables>();

            A.CallTo(() => fakeEnv.KubernetesServiceHost).Returns(host);
            A.CallTo(() => fakeEnv.KubernetesServicePort).Returns(port);
            A.CallTo(() => fakeEnv.KubernetesInClusterConfigOverwrite).Returns(kubernetesInClusterConfigOverwrite);

            _autoFake.Provide(fakeEnv);
            _autoFake.Provide(fakeFileSystem);

            var kClientFactory = _autoFake.Resolve<K8sClientFactory>();
            var config = kClientFactory.BuildInClusterConfigFromEnvironmentVariables();

            Assert.Equal($"https://{host}:{port}/", config.Host);
            Assert.Equal(tokenValue, config.AccessToken);
            Assert.Equal(2, config.SslCaCerts.Count);
        }

        [Fact]
        public void CreateFromKubeConfigTestWithProxy()
        {
            var fakeEnv = A.Fake<IEnvironmentVariables>();
            var expectedProxyUri = "http://localhost:8080/";
            A.CallTo(() => fakeEnv.KubectlProxy).Returns(expectedProxyUri);
            _autoFake.Provide(fakeEnv);
            var kClientFactory = _autoFake.Resolve<K8sClientFactory>();
            var config = kClientFactory.CreateFromKubeConfig(null);
            Assert.NotNull(config);
            Assert.Equal(expectedProxyUri, config.BaseUri.ToString());

        }

        [Fact]
        public void CreateFromKubeConfigTestWithOutProxy()
        {
            string expectedUri = "https://sample-demo.hcp.eastus.azmk8s.io/";
            IEnvironmentVariables fakeEnv = A.Fake<IEnvironmentVariables>();
            A.CallTo(() => fakeEnv.KubectlProxy).Returns(null);
            _autoFake.Provide(fakeEnv);
            K8sClientFactory kClientFactory = _autoFake.Resolve<K8sClientFactory>();
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
            K8SConfiguration fakeConfig = deserializer.Deserialize<K8SConfiguration>(File
                .ReadAllText(Path.Combine("TestData", "simpleconfig.yml")));
            IKubernetes config = kClientFactory.CreateFromKubeConfig(fakeConfig);
            Assert.NotNull(config);
            Assert.Equal(expectedUri, config.BaseUri.ToString());
        }
    }
}
