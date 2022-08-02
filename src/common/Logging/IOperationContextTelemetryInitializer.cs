// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.Extensibility;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal interface IOperationContextTelemetryInitializer : ITelemetryInitializer
    {
        void Initialize(ITelemetry telemetry, IOperationContext context, bool scramble = true);
    }
}