// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// ILogger for Application Insights
    /// </summary>
    internal class ApplicationInsightsLogger : LoggerBase
    {
        private readonly TelemetryClient _telClient;
        private readonly IEnumerable<IOperationContextTelemetryInitializer> _initializers;
        private readonly IApplicationInsightsLoggerConfig _config;

        public ApplicationInsightsLogger(
            IOperationContext context,
            TelemetryClient client,
            IApplicationInsightsLoggerConfig config, // We want to access rootscope-level settings for whether sending telemetry is allowed or not
            IEnumerable<IOperationContextTelemetryInitializer> initializers = null)
            : base(context)
        {
            this._telClient = client ?? throw new ArgumentNullException(nameof(client));
            this._initializers = initializers;
            this._config = config;
            this.LoggingVerbosity = config.ApplicationInsightsLoggingVerbosity;
        }

        #region LoggerBase overrides

        public override LoggerType LoggerType => LoggerType.Telemetry;
        protected override LoggingVerbosity LoggingVerbosity { get; }

        public bool ShouldScramblePii { get; set; }

        protected override void Write(EventLevel level, string line)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }
            var severity = SeverityLevel.Verbose;
            switch (level)
            {
                case EventLevel.LogAlways:
                case EventLevel.Critical:
                    severity = SeverityLevel.Critical;
                    break;

                case EventLevel.Error:
                    severity = SeverityLevel.Error;
                    break;

                case EventLevel.Warning:
                    severity = SeverityLevel.Warning;
                    break;

                case EventLevel.Informational:
                    severity = SeverityLevel.Information;
                    break;

                case EventLevel.Verbose:
                default:
                    severity = SeverityLevel.Verbose;
                    break;
            }

            try
            {
                var tracePayload = new TraceTelemetry(line, severity);
                TagWithInitializers(tracePayload);
                this._telClient.TrackTrace(tracePayload);
            }
            catch { }
        }

        protected override void RequestInner(HttpMethod httpMethod, Uri requestUri, long contentLength, PII requestBody)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }
            var bodyPayload = new EventTelemetry("Request.Start");
            bodyPayload.Properties["Uri"] = this.SanitizeUri(requestUri.ToString());
            bodyPayload.Properties["Method"] = httpMethod.Method;
            bodyPayload.Metrics["ContentLength"] = contentLength;
            TagWithInitializers(bodyPayload);
            this._telClient.TrackEvent(bodyPayload);
        }

        protected override void ResponseInner(HttpMethod httpMethod, Uri requestUri, MediaTypeHeaderValue contentType, long contentLength, long durationInMilliseconds, HttpStatusCode statusCode, PII responseBody)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }
            var sanitizedUri = this.SanitizeUri(requestUri.ToString());
            var requestPayload = new RequestTelemetry()
            {
                Id = OperationContext.RequestId,
                Duration = TimeSpan.FromMilliseconds(durationInMilliseconds),
                Name = $"{httpMethod} {sanitizedUri}",
                ResponseCode = statusCode.ToString(),
                Success = ((int)statusCode >= 200) && ((int)statusCode <= 299),
                Timestamp = OperationContext.StartTime != default(DateTimeOffset) ? OperationContext.StartTime : DateTimeOffset.UtcNow.AddMilliseconds(-durationInMilliseconds)
            };
            requestPayload.Properties["HttpMethod"] = httpMethod.Method;
            requestPayload.Properties["RequestUri"] = sanitizedUri;

            TagWithInitializers(requestPayload);

            this._telClient.TrackRequest(requestPayload);
        }

        protected override void ExceptionInner(Exception e, EventLevel level, bool handled = true)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }
            if (e == null)
            {
                this.Trace(EventLevel.Critical, "{0}: Null exception passed! StackTrace: {1}", nameof(Exception), Environment.StackTrace);
                return;
            }

            if (level <= EventLevel.Error)
            {
                this.TrackException(e);
            }
            else
            {
                this.Write(level, GetScrambledException(e).ToString());
            }
        }

        protected override void EventInner(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }
            try
            {
                var eventPayload = new EventTelemetry(eventName);
                properties.ExecuteForEach(property => { eventPayload.Properties[property.Key] = property.Value; });
                metrics.ExecuteForEach(metric => { eventPayload.Metrics[metric.Key] = metric.Value; });
                TagWithInitializers(eventPayload);
                _telClient.TrackEvent(eventPayload);
            }
            catch
            {
                Debug.Fail("Couldn't log event in Application Insights");
            }
        }

        protected override void DependencyInner(string name, string target, bool success, TimeSpan? duration = null, IDictionary<string, string> properties = null)
        {
            if (!IsTelemetryEnabled())
            {
                return;
            }
            try
            {
                var dependency = new DependencyTelemetry()
                {
                    Name = name,
                    Target = target,
                    Success = success,
                    Duration = duration ?? TimeSpan.Zero
                };
                properties.ExecuteForEach(p => { dependency.Properties[p.Key] = p.Value; });
                TagWithInitializers(dependency);
                _telClient.TrackDependency(dependency);
            }
            catch
            {
                Debug.Fail("Couldn't log dependency in Application Insights");
            }
        }

        protected override string GetScrambledValue(PII piiValue)
            => this.ShouldScramblePii ? piiValue.ScrambledValue : piiValue.Value;

        public override Task FlushAsync()
        {
            return Task.Run(() =>
            {
                _telClient.Flush();
            });
        }

        #endregion LoggerBase overrides

        #region Utilities

        private void TagWithInitializers<T>(T payload) where T : ITelemetry
        {
            _initializers.ExecuteForEach(i => i.Initialize(payload, OperationContext, ShouldScramblePii));
        }

        private void TrackException(Exception e)
        {
            var tel = new ExceptionTelemetry(GetScrambledException(e));
            TagWithInitializers(tel);
            this._telClient.TrackException(tel);
        }

        private bool IsTelemetryEnabled()
        {
            return _config?.IsTelemetryEnabledCallback?.Invoke() ?? false;
        }

        #endregion Utilities
    }
}