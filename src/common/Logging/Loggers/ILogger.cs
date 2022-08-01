// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Common interface for loggers
    /// </summary>
    internal interface ILogger : IService
    {
        LoggerType LoggerType { get; }

        /// <summary>
        /// Gets the current <see cref="IOperationContext"/>
        /// </summary>
        IOperationContext OperationContext { get; }

        /// <summary>Logs a trace</summary>
        void Trace(EventLevel eventLevel, string format, params object[] args);

        /// <summary>Logs an HTTP request</summary>
        void Request(HttpMethod httpMethod, Uri requestUri, long contentLength, PII requestBody, EventLevel eventLevel = EventLevel.Informational);

        /// <summary>Logs a response to an HTTP request</summary>
        void Response(HttpMethod httpMethod, Uri requestUri, MediaTypeHeaderValue contentType, long contentLength, long durationInMilliseconds, HttpStatusCode statusCode, PII responseBody, EventLevel eventLevel = EventLevel.Informational);

        /// <summary>Logs an exception</summary>
        void Exception(Exception e, bool handled = true);

        /// <summary>Logs an exception as a warning</summary>
        void ExceptionAsWarning(Exception e);

        /// <summary>Logs an event</summary>
        void Event(string eventName, IDictionary<string, object> properties = null, IDictionary<string, double> metrics = null, EventLevel eventLevel = EventLevel.Informational);

        /// <summary>Logs a call to a dependency</summary>
        void Dependency(string name, string target, bool success, TimeSpan? duration = null, IDictionary<string, object> properties = null);

        /// <summary>Flushes logger output buffer</summary>
        Task FlushAsync();
    }
}