// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Configuration settings for Application Insights logging
    /// </summary>
    internal interface IApplicationInsightsLoggerConfig
    {
        /// <summary>
        /// A function that returns a bool determining if AI Telemetry should be logged
        /// </summary>
        Func<bool> IsTelemetryEnabledCallback { get; set; }

        /// <summary>
        /// The verbosity of the AI logs
        /// </summary>
        public LoggingVerbosity ApplicationInsightsLoggingVerbosity { get; set; }
    }
}