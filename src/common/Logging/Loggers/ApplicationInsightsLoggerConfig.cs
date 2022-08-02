// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class ApplicationInsightsLoggerConfig : IApplicationInsightsLoggerConfig
    {
        private readonly IEnvironmentVariables _environmentVariables;

        public ApplicationInsightsLoggerConfig(IEnvironmentVariables environmentVariables)
        {
            this._environmentVariables = environmentVariables;
            this.IsTelemetryEnabledCallback = () => _environmentVariables.CollectTelemetry;
            this.ApplicationInsightsLoggingVerbosity = _environmentVariables.TelemetryVerbosity;
        }

        public Func<bool> IsTelemetryEnabledCallback { get; set; }
        public LoggingVerbosity ApplicationInsightsLoggingVerbosity { get; set; }
    }
}