// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.ApplicationInsights.Channel;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Add properties to telemetry based on the Operation Context information
    /// </summary>
    internal class OperationContextTelemetryInitializer : IOperationContextTelemetryInitializer
    {
        private IOperationContext _operationContext;
        internal static long SequenceNumber = 0;

        public OperationContextTelemetryInitializer(IOperationContext operationContext)
        {
            this._operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
        }

        public void Initialize(ITelemetry telemetry)
        {
            this.Initialize(telemetry, this._operationContext);
        }

        public void Initialize(ITelemetry telemetry, IOperationContext currentContext, bool scramble = true)
        {
            if (currentContext == null)
            {
                return;
            }

            telemetry.Sequence = Interlocked.Increment(ref SequenceNumber).ToString();

            // Set properties from context
            string userAgent = currentContext.UserAgent ?? "Unknown";

            telemetry.Context.Operation.Id = currentContext.RequestId;
            telemetry.Context.Operation.ParentId = currentContext.CorrelationId;
            telemetry.Context.GlobalProperties[Property.ClientRequestId] = currentContext.ClientRequestId;
            telemetry.Context.User.AccountId = currentContext.UserSubscriptionId;
            telemetry.Context.Device.Type = userAgent.Substring(0, Math.Min(userAgent.Length, 63));
            telemetry.Context.Component.Version = currentContext.Version;
            telemetry.Context.GlobalProperties[Property.UserAgent] = userAgent;

            // Scrub cloud_RoleInstance (when running client code locally it is the machine name e.g. LOLODI-HOME)
            telemetry.Context.Cloud.RoleInstance = new PII(Environment.MachineName).ScrambledValue;

            //Add additional LoggingProperties
            if (currentContext.LoggingProperties != null && currentContext.LoggingProperties.Any())
            {
                currentContext.LoggingProperties.ExecuteForEach(kv => { telemetry.Context.GlobalProperties[kv.Key] = PII.SanitizeOutput(kv.Value, pii => scramble ? pii.ScrambledValue : pii.Value); });
            }
        }
    }
}