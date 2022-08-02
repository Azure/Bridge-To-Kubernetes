// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Net;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.EndpointManager.Tests
{
    public class HostsFileManagerTests : TestsBase
    {
        private readonly HostsFileManager _hostsFileManager;

        private string _hostsFileContent = "#hosts file";
        private readonly string _hostsFileContent2 = $"10.20.30.0 foo.bar.com{Environment.NewLine}::1   localhost";
        private const string _workloadNamespace = "workload";

        private readonly HostsFileEntry _serviceA_workload_namespace_entry = new HostsFileEntry()
        {
            IP = "127.1.1.1",
            Names = new[] { "service-a", $"service-a.{_workloadNamespace}", $"service-a.{_workloadNamespace}.svc", $"service-a.{_workloadNamespace}.svc.cluster.local" }
        };
        private readonly string _serviceA_workload_namespace_line = $"127.1.1.1 service-a service-a.{_workloadNamespace} service-a.{_workloadNamespace}.svc service-a.{_workloadNamespace}.svc.cluster.local";

        private readonly HostsFileEntry _serviceA_different_namespace_entry = new HostsFileEntry()
        {
            IP = "127.1.1.2",
            Names = new[] { "service-a.different", $"service-a.different.svc", $"service-a.different.svc.cluster.local" }
        };
        private readonly string _serviceA_different_namespace_line = "127.1.1.2 service-a.different service-a.different.svc service-a.different.svc.cluster.local";

        private readonly HostsFileEntry _microsoft_com_external_entry = new HostsFileEntry()
        {
            IP = "127.1.1.3",
            Names = new[] { "microsoft.com" }
        };
        private readonly string _microsoft_com_external_line = "127.1.1.3 microsoft.com";

        public HostsFileManagerTests()
        {
            A.CallTo(() => _autoFake.Resolve<IPlatform>().IsWindows).Returns(true);

            A.CallTo(() => _autoFake.Resolve<IFileSystem>().FileExists(A<string>._)).Returns(true);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().OpenFileForWrite(A<string>._)).Returns(new MemoryStream());
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().ReadAllTextFromFile(A<string>._, A<int>._)).ReturnsLazily(() => _hostsFileContent);
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().WriteAllTextToFile(A<string>._, A<string>._, A<int>._)).Invokes((string name, string content, int maxAttempts) => _hostsFileContent = content);

            _hostsFileManager = _autoFake.Resolve<HostsFileManager>();
        }

        [Fact]
        public void EnsureAccess_Success()
        {
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().OpenFileForWrite(A<string>._)).Returns(new MemoryStream());

            _hostsFileManager.EnsureAccess();

            A.CallTo(() => _autoFake.Resolve<IFileSystem>().OpenFileForWrite(A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<ILog>().Info(A<string>._)).MustHaveHappenedTwiceOrMore();
        }

        [Fact]
        public void EnsureAccess_Throw()
        {
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().OpenFileForWrite(A<string>._)).Throws(() => new Exception());

            Assert.Throws<InvalidUsageException>(() => _hostsFileManager.EnsureAccess());

            A.CallTo(() => _autoFake.Resolve<ILog>().ExceptionAsWarning(A<Exception>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<ILog>().Error(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void AddEntry()
        {
            _hostsFileContent = string.Empty;
            _hostsFileManager.Add(_workloadNamespace, new[] { _serviceA_workload_namespace_entry, _serviceA_different_namespace_entry, _microsoft_com_external_entry });

            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_serviceA_different_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);
        }

        [Fact]
        public void AddDuplicateEntries()
        {
            _hostsFileManager.Add(_workloadNamespace, new[] { _serviceA_workload_namespace_entry, _serviceA_workload_namespace_entry });

            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);

            // Remove first instance of the hostsFileEntry line to make sure duplicate lines were filtered
            var index = _hostsFileContent.IndexOf(_serviceA_workload_namespace_line);
            _hostsFileContent = _hostsFileContent.Substring(0, index) + _hostsFileContent.Substring(index + _serviceA_workload_namespace_line.Length);
            Assert.DoesNotContain(_serviceA_workload_namespace_line, _hostsFileContent);
        }

        [Fact]
        public void AddEmpty()
        {
            _hostsFileContent = string.Empty;
            _hostsFileManager.Add(_workloadNamespace, new HostsFileEntry[] { });
            Assert.True(_hostsFileContent.Length == 0);
        }

        [Fact]
        public void InsertInsertCleanFile()
        {
            _hostsFileContent = string.Empty;

            _hostsFileManager.Add(_workloadNamespace, new[] { _serviceA_workload_namespace_entry, _microsoft_com_external_entry });

            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            var serviceANewIP = new HostsFileEntry() { IP = "127.1.1.4", Names = new[] { "service-a", $"service-a.{_workloadNamespace}", $"service-a.{_workloadNamespace}.svc", $"service-a.{_workloadNamespace}.svc.cluster.local" } };
            _hostsFileManager.Add(_workloadNamespace, new[] { serviceANewIP });

            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains($"127.1.1.4 service-a", _hostsFileContent);
            Assert.DoesNotContain(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);
        }

        [Fact]
        public void Remove()
        {
            _hostsFileContent = _hostsFileContent2;

            _hostsFileManager.Add(_workloadNamespace, new[] { _serviceA_workload_namespace_entry, _microsoft_com_external_entry });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            _hostsFileManager.Remove(new[] { IPAddress.Parse(_serviceA_workload_namespace_entry.IP) });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.DoesNotContain(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            // Remove A again, should be no-op
            _hostsFileManager.Remove(new[] { IPAddress.Parse(_serviceA_workload_namespace_entry.IP) });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.DoesNotContain(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            _hostsFileManager.Remove(new[] { IPAddress.Parse(_microsoft_com_external_entry.IP) });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.DoesNotContain($"# Added by {Product.Name}", _hostsFileContent);
            Assert.DoesNotContain(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.DoesNotContain(_microsoft_com_external_line, _hostsFileContent);
            Assert.DoesNotContain("# End of section", _hostsFileContent);
        }

        [Fact]
        public void Clear()
        {
            _hostsFileContent = _hostsFileContent2;

            _hostsFileManager.Add(_workloadNamespace, new[] { _serviceA_workload_namespace_entry });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            _hostsFileManager.Add(_workloadNamespace, new[] { _microsoft_com_external_entry });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            _hostsFileManager.Add(_workloadNamespace, new HostsFileEntry[] { });

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.Contains($"# Added by {Product.Name}", _hostsFileContent);
            Assert.Contains(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.Contains(_microsoft_com_external_line, _hostsFileContent);
            Assert.Contains("# End of section", _hostsFileContent);

            _hostsFileManager.Clear();

            Assert.Contains(_hostsFileContent2, _hostsFileContent);
            Assert.DoesNotContain($"# Added by {Product.Name}", _hostsFileContent);
            Assert.DoesNotContain(_serviceA_workload_namespace_line, _hostsFileContent);
            Assert.DoesNotContain(_microsoft_com_external_line, _hostsFileContent);
            Assert.DoesNotContain("# End of section", _hostsFileContent);
        }

        [Fact]
        public void AddInvalidServiceEntry()
        {
            var invalid_entry = new HostsFileEntry()
            {
                IP = "127.1.1.1",
                Names = new[] { "Ⅻㄨㄩ 啊阿鼾齄丂丄狚狛狜狝﨨﨩ˊˋ˙–⿻〇" }
            };
            Assert.Throws<InvalidOperationException>(() => _hostsFileManager.Add(_workloadNamespace, new[] { invalid_entry }));
        }

        [Fact]
        public void AddInvalidIPsegmentsEntry()
        {
            var invalid_entry = new HostsFileEntry()
            {
                IP = "127.1.1.1.1",
                Names = new[] { "service-a" }
            };
            Assert.Throws<InvalidOperationException>(() => _hostsFileManager.Add(_workloadNamespace, new[] { invalid_entry }));
        }

        [Fact]
        public void AddInvalidIPCharsEntry()
        {
            var invalid_entry = new HostsFileEntry()
            {
                IP = "127.1.a.1",
                Names = new[] { "service-a" }
            };
            Assert.Throws<InvalidOperationException>(() => _hostsFileManager.Add(_workloadNamespace, new[] { invalid_entry }));
        }
    }
}