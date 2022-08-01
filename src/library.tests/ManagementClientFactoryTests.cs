// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class ManagementClientFactoryTests : TestsBase
    {
        [Fact]
        public void TelemetryCallbackPropagationTest()
        {
            Func<bool> telemetryCallback = () => true;
            ManagementClientFactory.IsTelemetryEnabledCallback = telemetryCallback;
            Assert.Equal(telemetryCallback, AppContainerConfig.RootScope.Resolve<IApplicationInsightsLoggerConfig>().IsTelemetryEnabledCallback);
        }
    }
}