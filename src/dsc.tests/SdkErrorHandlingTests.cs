// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using FakeItEasy;
using k8s.Autorest;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Exe.Tests
{
    public class SdkErrorHandlingTests : TestsBase
    {
        public static readonly List<object[]> _messageTestCollection = new List<object[]>()
        {
            // <exception to throw>
            // <expected console output>
            new object[]
            {
                new OperationCanceledException(),
                Resources.Error_OperationCanceled
            },
            new object[]
            {
                new TestInvalidUsageException("message"),
                "message"
            }
        };

        public static readonly List<object[]> _failedDependenciesCollection = new List<object[]>()
        {
            new object[] { new TestDependencyException() },
            new object[] { new Exception("outer", new TestDependencyException()) }
        };

        private readonly SdkErrorHandling _sdkErrorHandling;

        public SdkErrorHandlingTests()
        {
            _sdkErrorHandling = _autoFake.Resolve<SdkErrorHandling>();
        }

        [Theory]
        [MemberData(nameof(_messageTestCollection))]
        public void HandlingTests(Exception e, string message)
        {
            Assert.True(_sdkErrorHandling.TryHandleKnownException(e, null, out string failureReason));

            A.CallTo(() => _autoFake.Resolve<IConsoleOutput>().Error(message, A<bool>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IConsoleOutput>().Error(A<string>.That.Not.IsEqualTo(message), A<bool>._))
                .MustNotHaveHappened();
            Assert.Empty(Fake.GetCalls(_autoFake.Resolve<IConsoleOutput>()).Where(c => c.Method.Name != nameof(IConsoleOutput.Error)));

            // Ensure only a single log was done
            Assert.Single(Fake.GetCalls(_autoFake.Resolve<ILog>()).Where(c => c.Method.Name.IsIn(new[] { nameof(ILog.Error), nameof(ILog.Warning) })));
        }

        [Fact]
        public void InnerExceptionsGetProcessed()
        {
            var ex = new Exception("outer", new OperationCanceledException("inner"));
            string expectedMessage = Resources.Error_OperationCanceled;
            Assert.True(_sdkErrorHandling.TryHandleKnownException(ex, null, out string failureReason));

            A.CallTo(() => _autoFake.Resolve<IConsoleOutput>().Error(expectedMessage, A<bool>._))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _autoFake.Resolve<IConsoleOutput>().Error(A<string>.That.Not.IsEqualTo(expectedMessage), A<bool>._))
                .MustNotHaveHappened();
        }

        [Theory]
        [MemberData(nameof(_failedDependenciesCollection))]
        public void FailedDependenciesGetLogged(Exception e)
        {
            Assert.False(_sdkErrorHandling.TryHandleKnownException(e, "failedDependency", out string failureReason));

            A.CallTo(() => _autoFake.Resolve<IConsoleOutput>().Error(A<string>._, A<bool>._))
                .MustNotHaveHappened();
            A.CallTo(() => _autoFake.Resolve<ILog>().Dependency(
                    "failedDependency",
                    "http://foobar/",
                    false,
                    A<TimeSpan?>._,
                    A<IDictionary<string, object>>.That.Matches(d =>
                        d["RequestId"] as string == "reqId" &&
                        d["ClientRequestId"] as string == "clientReqId" &&
                        d["CorrelationRequestId"] as string == "correlationReqId")))
                .MustHaveHappenedOnceExactly();
        }

        private class TestDependencyException : Exception, IRequestResponse, IOperationIds
        {
            public HttpRequestMessageWrapper Request { get; } = new HttpRequestMessageWrapper(new HttpRequestMessage(HttpMethod.Get, "http://foobar/"), "requestdata");
            public HttpResponseMessageWrapper Response { get; } = new HttpResponseMessageWrapper(new HttpResponseMessage(), "responsedata");
            public string RequestId { get; } = "reqId";
            public string ClientRequestId { get; } = "clientReqId";
            public string CorrelationRequestId { get; } = "correlationReqId";
        }
    }
}