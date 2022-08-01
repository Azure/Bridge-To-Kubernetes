// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.Tracing;
using System.Net.Http;
using System.Net.Http.Headers;
using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Logging
{
    public class ApplicationInsightsLoggerTests : TestsBase
    {
        [Theory]
        [InlineData("trace")]
        [InlineData("request")]
        [InlineData("response")]
        [InlineData("exception")]
        [InlineData("event")]
        [InlineData("dependency")]
        public void TelemetryCallbackIsRespected(string loggerMethod)
        {
            var config = _autoFake.Resolve<IApplicationInsightsLoggerConfig>();
            config.IsTelemetryEnabledCallback = () => true;
            config.ApplicationInsightsLoggingVerbosity = LoggingVerbosity.Verbose;
            var aiLogger = _autoFake.Resolve<ApplicationInsightsLogger>();

            switch (loggerMethod)
            {
                case "trace":
                    aiLogger.Trace(EventLevel.Informational, "an event occurred");
                    break;

                case "request":
                    aiLogger.Request(HttpMethod.Get, new Uri("http://www.contoso.com/"), 0, new PII("request body"));
                    break;

                case "response":
                    aiLogger.Response(HttpMethod.Get, new Uri("http://www.contoso.com/"), new MediaTypeHeaderValue("text/html"), 0, 0, System.Net.HttpStatusCode.OK, new PII("response body"));
                    break;

                case "exception":
                    aiLogger.Exception(new ArgumentNullException());
                    break;

                case "event":
                    aiLogger.Event("an event occurred");
                    break;

                case "dependency":
                    aiLogger.Dependency("an operation", "a target", true);
                    break;
            }

            A.CallTo(() => config.IsTelemetryEnabledCallback).MustHaveHappened();
        }
    }
}