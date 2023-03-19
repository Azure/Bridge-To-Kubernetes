// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using System.Linq;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.PersistentProperyBag;
using Microsoft.BridgeToKubernetes.Common.Serialization;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class ClientConfigTests :  TestsBase
    {
        private static string configFileDirectoryPath = Path.Combine(@"USER_SETTINGS_PATH", Common.Constants.DirectoryName.PersistedFiles);
        private static string configFilePath = Path.Combine(configFileDirectoryPath, Common.Constants.FileNames.Config);
        private static string configFileBackupPath = configFilePath + ".bak";
        private const string persistedMac = "bc69b0dca17877e5acb2d6d6af92bf025143c8d751206c2490deb528541d51a8";
        private const string persistedMacKey = "mac.address";
        private const string intPropertyKey = "intProperty";
        private const int intPropertyValue = 42;
        private string configFileSampleData = $@"{{""{persistedMacKey}"":""{persistedMac}""}}";

        public ClientConfigTests()
        {
            _autoFake.Provide<IJsonSerializer>(new JsonSerializer());

            A.CallTo(() => _autoFake.Resolve<IFileSystem>().Path).Returns(_autoFake.Resolve<PathUtilities>());
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().GetPersistedFilesDirectory(Common.Constants.DirectoryName.PersistedFiles)).Returns(configFileDirectoryPath);
        }

        [Fact]
        public void LoadEmptyStorage()
        {
            var clientConfig = _autoFake.Resolve<ClientConfig>();
            Assert.Empty(clientConfig.GetAllProperties());
        }

        [Fact]
        public void LoadExistingStorage()
        {
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFilePath)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(configFilePath, A<int>._)).Returns(configFileSampleData);

            var clientConfig = _autoFake.Resolve<ClientConfig>();
            Assert.Single(clientConfig.GetAllProperties().ToList());
            Assert.Equal(persistedMac, clientConfig.GetProperty(persistedMacKey));
        }

        [Fact]
        public void LoadFromBackup()
        {
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFilePath)).Returns(false);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFileBackupPath)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(configFileBackupPath, A<int>._)).Returns(configFileSampleData);

            var clientConfig = _autoFake.Resolve<ClientConfig>();
            Assert.Single(clientConfig.GetAllProperties().ToList());
            Assert.Equal(persistedMac, clientConfig.GetProperty(persistedMacKey));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().WriteAllTextToFile(configFilePath, configFileSampleData, A<int>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void LoadWithMissingBackup()
        {
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFilePath)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(configFilePath, A<int>._)).Returns(configFileSampleData);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFileBackupPath)).Returns(false);

            var clientConfig = _autoFake.Resolve<ClientConfig>();
            Assert.Single(clientConfig.GetAllProperties().ToList());
            Assert.Equal(persistedMac, clientConfig.GetProperty(persistedMacKey));
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().WriteAllTextToFile(configFileBackupPath, configFileSampleData, A<int>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void RemoveProperty()
        {
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFilePath)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(configFilePath, A<int>._)).Returns(configFileSampleData);

            var clientConfig = _autoFake.Resolve<ClientConfig>();
            clientConfig.RemoveProperty(persistedMacKey);
            Assert.Empty(clientConfig.GetAllProperties());
        }

        [Fact]
        public void ClearAndPersist()
        {
            string sampleDate = configFileSampleData;
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFilePath)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(configFilePath, A<int>._)).ReturnsLazily(() => configFileSampleData);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().WriteAllTextToFile(configFilePath, A<string>._, A<int>._)).Invokes((string path, string contents) => configFileSampleData = contents);

            var clientConfig = _autoFake.Resolve<ClientConfig>();
            clientConfig.Clear();
            Assert.Empty(clientConfig.GetAllProperties());
            clientConfig.Persist();
            clientConfig = _autoFake.Resolve<ClientConfig>();
            Assert.Empty(clientConfig.GetAllProperties());
        }

        [Fact]
        public void SetAndPersist()
        {
            string sampleDate = configFileSampleData;
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(configFilePath)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(configFilePath, A<int>._)).ReturnsLazily(() => configFileSampleData);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().WriteAllTextToFile(configFilePath, A<string>._, A<int>._)).Invokes((string path, string contents) => configFileSampleData = contents);

            var clientConfig = _autoFake.Resolve<ClientConfig>();
            clientConfig.SetProperty(intPropertyKey, intPropertyValue);
            Assert.Equal(2, clientConfig.GetAllProperties().ToList().Count);
            Assert.Equal(intPropertyValue, clientConfig.GetProperty(intPropertyKey));
            Assert.Equal(persistedMac, clientConfig.GetProperty(persistedMacKey));
            clientConfig.Persist();
            clientConfig = _autoFake.Resolve<ClientConfig>();
            Assert.Equal(2, clientConfig.GetAllProperties().ToList().Count);
            Assert.Equal(intPropertyValue, clientConfig.GetProperty(intPropertyKey));
            Assert.Equal(persistedMac, clientConfig.GetProperty(persistedMacKey));
        }
    }
}