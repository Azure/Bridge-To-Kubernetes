// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class LocalProcessConfigTests
    {
        private IFileSystem _fileSystem = A.Fake<IFileSystem>();
        private ILog _log = A.Fake<ILog>();

        public LocalProcessConfigTests()
        {
        }

        [Fact]
        public void ParseConfigFile()
        {
            A.CallTo(() => _fileSystem.ReadAllTextFromFile(A<string>._, A<int>._)).Returns(KubernetesLocalProcessConfig);
            LocalProcessConfig result = new LocalProcessConfig("file.yaml", _fileSystem, _log);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.ReferencedVolumes.Count());
            Assert.Equal(3, result.ReferencedServices.Count());
            Assert.Equal(2, result.ReferencedExternalEndpoints.Count());

            var v1 = result.ReferencedVolumes.First();
            Assert.Equal("whitelist", v1.Name);
            Assert.Equal("", v1.LocalPath);

            var v2 = result.ReferencedVolumes.Last();
            Assert.Equal("azds-cert", v2.Name);
            Assert.Equal("", v2.LocalPath);

            var s1 = result.ReferencedServices.First();
            Assert.Equal("geneva-infra.infra", s1.Name);
            Assert.Empty(s1.Ports);

            var s2 = result.ReferencedServices.Last();
            Assert.Equal("identity", s2.Name);
            Assert.Empty(s2.Ports);

            var e1 = result.ReferencedExternalEndpoints.First();
            Assert.Equal("myteststorage.blob.core.windows.net", e1.Name);
            Assert.Equal(2, e1.Ports.Length);
            Assert.Equal(80, e1.Ports[0]);
            Assert.Equal(443, e1.Ports[1]);

            var e2 = result.ReferencedExternalEndpoints.Last();
            Assert.Equal("server-sibaelius.database.windows.net", e2.Name);
            Assert.Single(e2.Ports);
            Assert.Equal(1433, e2.Ports[0]);
        }

        [Fact]
        public void ParseConfigFileWithVolumeMount()
        {
            A.CallTo(() => _fileSystem.ReadAllTextFromFile(A<string>._, A<int>._)).Returns(KubernetesLocalProcessConfigWithVolumeMount);
            LocalProcessConfig result = new LocalProcessConfig("file.yaml", _fileSystem, _log);
            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.ReferencedVolumes.Count());
            Assert.Equal("/foo/bar", result.ReferencedVolumes.Single(v => v.Name == "whitelist").LocalPath);
            Assert.True(string.IsNullOrEmpty(result.ReferencedVolumes.Single(v => v.Name == "azds-cert").LocalPath));
        }

        [Theory]
        [InlineData(KubernetesLocalProcessConfigTooManyServicePorts, 1)]
        [InlineData(KubernetesLocalProcessConfigMissingPortsForExternalEndpoint, 1)]
        [InlineData(KubernetesLocalProcessConfigMultipleIssues, 2)]
        public void ParseConfigFileWithIssues(string localProcessConfig, int expecteNumIssues)
        {
            A.CallTo(() => _fileSystem.ReadAllTextFromFile(A<string>._, A<int>._)).Returns(localProcessConfig);
            LocalProcessConfig result = new LocalProcessConfig("file.yaml", _fileSystem, _log);
            Assert.False(result.IsSuccess);
            var issues = result.AllIssues;
            Assert.Equal(expecteNumIssues, result.AllIssues.Count());
        }

        [Fact]
        public void VersionNotSupported()
        {
            var version = new Version(LocalProcessConfig.LatestSupportedVersion.Major + 1, 0);
            string config = KubernetesLocalProcessConfig.Replace("version: 0.1", $"version: {version}");
            A.CallTo(() => _fileSystem.ReadAllTextFromFile(A<string>._, A<int>._)).Returns(config);

            LocalProcessConfig result = new LocalProcessConfig("file.yaml", _fileSystem, _log);
            Assert.False(result.IsSuccess);
            Assert.Contains("unsupported version", result.AllIssues.First().Message);
        }

        [Fact]
        public void VersionMissing()
        {
            string config = KubernetesLocalProcessConfig.Replace("version: 0.1", string.Empty);
            A.CallTo(() => _fileSystem.ReadAllTextFromFile(A<string>._, A<int>._)).Returns(config);

            LocalProcessConfig result = new LocalProcessConfig("file.yaml", _fileSystem, _log);
            Assert.False(result.IsSuccess);
            Assert.Contains("does not specify a version", result.AllIssues.First().Message);
        }

        private const string KubernetesLocalProcessConfig =
            @"
            # some comment here
            # an empty line underneath

            # some more comments
            # now the real content:

            version: 0.1
            env:
              - name: WHITELIST_PATH    # random comment
                value: $(volumeMounts:whitelist)/whitelist
              - name: AZDS_SERVICE_PRINCIPAL_PFX_PATH
                value: $(volumeMounts:azds-cert)/secrets/azds
              # another random comment

              - name: GENEVA_SERVICE_HOST
                value: $(services:geneva-infra.infra)
              - name: KAFKA_BROKER
                value: $(services:kafka-headless.kafka)
              - name: MY_SERVICE
                value: $(services:identity)
              - name: STORAGE_SERVICE
                value: $(externalEndpoints:myteststorage.blob.core.windows.net:80,443)
              - name: DB_HOST
                value: $(externalEndpoints:server-sibaelius.database.windows.net:1433)";

        private const string KubernetesLocalProcessConfigWithVolumeMount =
            @"
            version: 0.1
            env:
              - name: WHITELIST_PATH    # random comment
                value: $(volumeMounts:whitelist)/whitelist
              - name: AZDS_SERVICE_PRINCIPAL_PFX_PATH
                value: $(volumeMounts:azds-cert)/secrets/azds
            volumeMounts:
              - name: whitelist
                localPath: /foo/bar";

        private const string KubernetesLocalProcessConfigMultipleIssues =
        @"
            # some comment here
            # an empty line underneath

            # some more comments
            # now the real content:

            version: 0.1
            env:
              - name: WHITELIST_PATH
                value: $(volumeMounts:whitelist)/whitelist

            # and more comments and empty lines and real content

              - name: GENEVA_SERVICE_HOST
                value: $(services:geneva-infra.infra:12:34)
              - name: STORAGE_SERVICE
                value: $(externalEndpoints:myteststorage.blob.core.windows.net)";

        private const string KubernetesLocalProcessConfigTooManyServicePorts =
            @"
            # some comment here
            # an empty line underneath

            # some more comments
            # now the real content:

            version: 0.1
            env:
              - name: WHITELIST_PATH
                value: $(volumeMounts:whitelist)/whitelist

            # and more comments and empty lines and real content

              - name: AZDS_SERVICE_PRINCIPAL_PFX_PATH
                value: $(volumeMounts:azds-cert)/secrets/azds
              - name: GENEVA_SERVICE_HOST
                value: $(services:geneva-infra.infra:12:34)";

        private const string KubernetesLocalProcessConfigMissingPortsForExternalEndpoint =
        @"
            # some comment here
            # an empty line underneath

            # some more comments
            # now the real content:

            version: 0.1
            env:
              - name: WHITELIST_PATH
                value: $(volumeMounts:whitelist)/whitelist

            # and more comments and empty lines and real content

            version: 0.1
            env:
              - name: STORAGE_SERVICE
                value: $(externalEndpoints:myteststorage.blob.core.windows.net)";
    }
}