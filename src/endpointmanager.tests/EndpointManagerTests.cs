// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.IP;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Models.Settings;
using Microsoft.BridgeToKubernetes.Common.Socket;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.EndpointManager.Tests
{
    public class EndpointManagerTests : TestsBase
    {
        private readonly EndpointManager _endpointManager;

        public EndpointManagerTests()
        {
            A.CallTo(() => _autoFake.Resolve<IPlatform>().IsWindows).Returns(true);

            _endpointManager = _autoFake.Resolve<EndpointManager>();
        }

        [Fact]
        public void AddHostsFileEntry()
        {
            var request = new EndpointManagerRequest<AddHostsFileEntryArgument>()
            {
                ApiName = Constants.EndpointManager.ApiNames.AddHostsFileEntry.ToString(),
                CorrelationId = "1234",
                Argument = new AddHostsFileEntryArgument { WorkloadNamespace = "Default", Entries = new List<HostsFileEntry> { new HostsFileEntry() { IP = "1.1.1.1", Names = new[] { "service-a" } } } }
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Add(A<string>._, A<IEnumerable<HostsFileEntry>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeSocket.SendWithEndMarkerAsync(A<string>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void AllocateIP()
        {
            var request = new EndpointManagerRequest<AllocateIPArgument>()
            {
                ApiName = Constants.EndpointManager.ApiNames.AllocateIP.ToString(),
                Argument = new AllocateIPArgument { Endpoints = new[] { new EndpointInfo { Ports = new[] { new PortPair(80, 8000) } } } }
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeSocket.SendWithEndMarkerAsync(A<string>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void DisableService()
        {
            var request = new EndpointManagerRequest<DisableServiceArgument>()
            {
                ApiName = Constants.EndpointManager.ApiNames.DisableService.ToString(),
                Argument = new DisableServiceArgument { ServicePortMappings = new[] { new ServicePortMapping("BranchCache", 80, 77) } }
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IServiceController>().Stop()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IServiceController>().WaitForStatus(A<ServiceControllerStatus>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeSocket.SendWithEndMarkerAsync(A<string>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void DisableService_InvalidService()
        {
            var request = new EndpointManagerRequest<DisableServiceArgument>()
            {
                ApiName = Constants.EndpointManager.ApiNames.DisableService.ToString(),
                Argument = new DisableServiceArgument { ServicePortMappings = new[] { new ServicePortMapping("NotASystemService", 80, 77) } }
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IServiceController>().Stop()).MustNotHaveHappened();
            A.CallTo(() => _autoFake.Resolve<IServiceController>().WaitForStatus(A<ServiceControllerStatus>._)).MustNotHaveHappened();
            A.CallTo(() => fakeSocket.SendWithEndMarkerAsync(A<string>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void FreeIP()
        {
            var request = new EndpointManagerRequest<FreeIPArgument>()
            {
                ApiName = Constants.EndpointManager.ApiNames.FreeIP.ToString(),
                Argument = new FreeIPArgument { IPAddresses = new IPAddress[] { IPAddress.Parse("1.1.1.1"), IPAddress.Parse("2.2.2.2") } }
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IIPManager>().FreeIPs(A<IPAddress[]>._, A<IHostsFileManager>._, A<bool>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void KillProcess()
        {
            var request = new EndpointManagerRequest<KillProcessArgument>()
            {
                ApiName = Constants.EndpointManager.ApiNames.KillProcess.ToString(),
                Argument = new KillProcessArgument { ProcessPortMappings = new[] { new ProcessPortMapping("process1", 80, 77) } }
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IPlatform>().KillProcess(77)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void Ping()
        {
            var request = new EndpointManagerRequest()
            {
                ApiName = Constants.EndpointManager.ApiNames.Ping.ToString()
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        [Fact]
        public void SystemCheck()
        {
            var request = new EndpointManagerRequest()
            {
                ApiName = Constants.EndpointManager.ApiNames.SystemCheck.ToString()
            };
            var fakeSocket = ExecuteApiCall(request);

            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().EnsureAccess()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IFileSystem>().EnsureDirectoryDeleted(A<string>._, A<bool>._, A<ILog>._)).MustHaveHappenedTwiceOrMore();
            A.CallTo(() => fakeSocket.AcceptAsync()).MustHaveHappenedTwiceExactly(); // Once for execution, second time to trigger shutdown
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IHostsFileManager>().Clear()).MustHaveHappenedOnceExactly();
        }

        /// <summary>
        /// Set up mechanism for triggering an API command, then executes it
        /// </summary>
        /// <returns>A fake ISocket that will be used to transmit the API call, and can be used for .MustHaveHappened* assertions</returns>
        private ISocket ExecuteApiCall<T>(T request) where T : EndpointManagerRequest
        {
            var requestBytes = Encoding.UTF8.GetBytes(JsonHelpers.SerializeObject(request));

            var fakeSocket = _autoFake.Resolve<ISocket>();
            A.CallTo(() => fakeSocket.ReadUntilEndMarkerAsync())
                .ReturnsLazily(() => Task.FromResult(JsonHelpers.SerializeObject(request))).Once();
            A.CallTo(() => _autoFake.Resolve<ISocket>().AcceptAsync())
                .Returns(fakeSocket).Once()
                .Then
                .Throws(new Exception("Aborting socket to end test"));

            // Execute the API call on EndpointManager
            _endpointManager.Execute(new string[] { "dummyUser", "dummySocketFile", "dummyLogFileDirectory", "dummyCorrelationId" }, default(CancellationToken));

            return fakeSocket;
        }
    }
}