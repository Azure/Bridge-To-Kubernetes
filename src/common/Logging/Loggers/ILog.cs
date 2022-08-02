// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Manage and send messages to registered loggers.
    /// </summary>
    internal interface ILog : ILogger
    {
        /// <summary>
        /// Returns a Log instance the same as this one except that Telemetry type loggers are excluded. Log operations
        /// like .Trace() or .Flush() on this instance will be executed on all loggers EXCEPT those of
        /// LoggerType.Telemetry (e.g. Application Insights). Typically used to write logs on the client without sending
        /// unnecessary telemetry, or to avoid slow explicit flushes to Application Insights.
        /// </summary>
        ILog WithoutTelemetry { get; }

        /// <summary>
        /// Returns a Log instance the same as this one except that Console type loggers are excluded.
        /// </summary>
        ILog WithoutConsole { get; }

        /// <summary>Logs a critical message to all registered loggers</summary>
        /// <remarks>If the first arg is an IOperationContext, the operation context is overridden for the log call</remarks>
        void Critical(string format, params object[] args);

        /// <summary>Logs a warning message to all registered loggers</summary>
        /// <remarks>If the first arg is an IOperationContext, the operation context is overridden for the log call</remarks>
        void Warning(string format, params object[] args);

        /// <summary>Logs a verbose message to all registered loggers</summary>
        /// <remarks>If the first arg is an IOperationContext, the operation context is overridden for the log call</remarks>
        void Verbose(string format, params object[] args);

        /// <summary>Logs an informational message to all registered loggers</summary>
        /// <remarks>If the first arg is an IOperationContext, the operation context is overridden for the log call</remarks>
        void Info(string format, params object[] args);

        /// <summary>Logs an error message to all registered loggers</summary>
        /// <remarks>If the first arg is an IOperationContext, the operation context is overridden for the log call</remarks>
        void Error(string format, params object[] args);

        /// <summary>Creates a <see cref="IPerformanceLogger"/> to measure the duration of an operation and log the result.</summary>
        IPerformanceLogger StartPerformanceLogger(string areaName, string metricName, IDictionary<string, object> properties = null, EventLevel eventLevel = EventLevel.Informational);

        /// <summary>
        /// Removes loggers of the specified type
        /// </summary>
        /// <param name="loggerType">The logger type to remove</param>
        void RemoveLoggerType(LoggerType loggerType);

        /// <summary>Flushes all registered loggers</summary>
        /// <param name="timeout">Maximum duration to wait for flush to complete</param>
        void Flush(TimeSpan timeout);
    }
}