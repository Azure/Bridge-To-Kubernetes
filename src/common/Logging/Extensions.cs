// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using Microsoft.AspNetCore.Http;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;
using System.Net.Http;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal static class Extensions
    {
        /// <summary>
        /// Returns a formatted string. Any exceptions are swallowed and logged. Args are serialized before being substituted into the string.
        /// </summary>
        /// <param name="log"></param>
        /// <param name="format">Composite format string</param>
        /// <param name="args">Objects to substitute into the composite format string</param>
        /// <returns>A copy of <paramref name="format"/> in which the format items have been replaced by the string representation of the corresponding objects in <paramref name="args"/>.</returns>
        public static string SaferFormat(this ILogger log, string format, params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return format;
            }

            try
            {
                var serializedArgs = args.Select(arg => JsonHelpers.SerializeForLoggingPurpose(arg)).ToArray();
                return string.Format(CultureInfo.InvariantCulture, format, serializedArgs);
            }
            catch (Exception ex)
            {
                log.Exception(ex);
                var message = $"{format} -- {args.Aggregate((x, y) => $"{x ?? "null"} -- {y ?? "null"}")}";
                log.Trace(EventLevel.Error, $"Failed to format: {message}");
                return message;
            }
        }

        /// <summary>
        /// Scrambles and add PII markers to the header values.
        /// </summary>
        /// <param name="headers">Request headers</param>
        /// <param name="addPIIMarkersDelegate">If this is not null, any header that is not in the <see cref="LoggingConstants.AllowedNonPIIRequestHeaders"/> list is replaced by the value returned by the delegate</param>
        /// <returns></returns>
        public static IDictionary<string, IEnumerable<string>> ScrambleAndAddPIIMarkersToHeaders(this IDictionary<string, IEnumerable<string>> headers, Func<PII, string> addPIIMarkersDelegate = null)
        {
            if (headers == null || !headers.Any())
            {
                return headers;
            }
            var caseInsensitiveHeaders = new Dictionary<string, IEnumerable<string>>(headers, StringComparer.OrdinalIgnoreCase);
            foreach (var header in headers.Keys)
            {
                if (LoggingConstants.HeadersToScramble.Contains(header, StringComparer.OrdinalIgnoreCase) && caseInsensitiveHeaders.TryGetValue(header, out var headerValuesToScramble))
                {
                    // These are the values we don't want to log or display in plaintext, even in Jarvis
                    caseInsensitiveHeaders[header] = headerValuesToScramble.Select(value => (new PII(value)).ScrambledValue);
                }
                else if (addPIIMarkersDelegate != null &&
                    !LoggingConstants.AllowedNonPIIRequestHeaders.Contains(header, StringComparer.OrdinalIgnoreCase) &&
                    caseInsensitiveHeaders.TryGetValue(header, out var headerValues))
                {
                    // We want to mark PII values with start and end markers with the delegate provided for any header not in the allowed list
                    // These will get dropped by Geneva en route to Cosmos, but we will still be able to see the values in Jarvis
                    caseInsensitiveHeaders[header] = headerValues.Select(value => addPIIMarkersDelegate(new PII(value)));
                }
            }

            return caseInsensitiveHeaders;
        }

        public static void ScrambleHeaders(this IDictionary<string, IEnumerable<string>> headers)
        {
            var scrubbedHeaders = headers.ScrambleAndAddPIIMarkersToHeaders();
            headers.Clear();
            scrubbedHeaders.ExecuteForEach(h => headers.Add(h));
        }

        /// <summary>
        /// Determines whether a given EventLevel meets a LoggingVerbosity's threshold.
        /// </summary>
        /// <param name="verbosity">Verbosity level to check against</param>
        /// <param name="level">Severity of an event or message</param>
        /// <returns>True if <paramref name="level"/> is severe enough to be output, otherwise false</returns>
        public static bool Includes(this LoggingVerbosity verbosity, EventLevel level)
        {
            var minimumVerbosity = LoggingVerbosity.Verbose;
            switch (level)
            {
                case EventLevel.LogAlways:
                case EventLevel.Critical:
                case EventLevel.Error:
                    minimumVerbosity = LoggingVerbosity.Quiet;
                    break;

                case EventLevel.Warning:
                case EventLevel.Informational:
                    minimumVerbosity = LoggingVerbosity.Normal;
                    break;

                case EventLevel.Verbose:
                default:
                    minimumVerbosity = LoggingVerbosity.Verbose;
                    break;
            }

            return verbosity >= minimumVerbosity;
        }

        #region headersFromHttpRequestMessage

        public static string GetClientRequestId(this HttpRequestMessage operationRequest)
        {
            IEnumerable<string> clientRequestIds = new List<string>();
            operationRequest?.Headers?.TryGetValues(Constants.CustomHeaderNames.ClientRequestId, out clientRequestIds);
            return clientRequestIds?.FirstOrDefault();
        }

        #endregion headersFromHttpRequestMessage

        #region headersFromHttpResponseMessage

        public static string GetRequestId(this HttpResponseMessage operationResponse)
        {
            IEnumerable<string> requestIds = new List<string>();
            operationResponse?.Headers?.TryGetValues(Constants.CustomHeaderNames.RequestId, out requestIds);
            return requestIds?.FirstOrDefault();
        }

        public static string GetCorrelationRequestId(this HttpResponseMessage operationResponse)
        {
            IEnumerable<string> correlationRequestIds = new List<string>();
            operationResponse?.Headers?.TryGetValues(Constants.CustomHeaderNames.CorrelationRequestId, out correlationRequestIds);
            return correlationRequestIds?.FirstOrDefault();
        }

        #endregion headersFromHttpResponseMessage

        #region headersFromHttpRequestMessageWrapper

        public static string GetClientRequestId(this HttpRequestMessageWrapper operationRequest)
        {
            IEnumerable<string> clientRequestIds = new List<string>();
            operationRequest?.Headers?.TryGetValue(Constants.CustomHeaderNames.ClientRequestId, out clientRequestIds);
            return clientRequestIds?.FirstOrDefault();
        }

        #endregion headersFromHttpRequestMessageWrapper

        #region headersFromHttpResponseMessageWrapper

        public static string GetRequestId(this HttpResponseMessageWrapper operationResponse)
        {
            IEnumerable<string> requestIds = new List<string>();
            operationResponse?.Headers?.TryGetValue(Constants.CustomHeaderNames.RequestId, out requestIds);
            return requestIds?.FirstOrDefault();
        }

        public static string GetCorrelationRequestId(this HttpResponseMessageWrapper operationResponse)
        {
            IEnumerable<string> correlationRequestIds = new List<string>();
            operationResponse?.Headers?.TryGetValue(Constants.CustomHeaderNames.CorrelationRequestId, out correlationRequestIds);
            return correlationRequestIds?.FirstOrDefault();
        }

        #endregion headersFromHttpResponseMessageWrapper

        #region headersFromHttpHeaders

        public static string GetClientRequestId(this IHeaderDictionary headers)
        {
            StringValues clientRequestIds = new StringValues();
            headers?.TryGetValue(Constants.CustomHeaderNames.ClientRequestId, out clientRequestIds);
            return clientRequestIds.FirstOrDefault();
        }

        #endregion headersFromHttpHeaders

    }
}