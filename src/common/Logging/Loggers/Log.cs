// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Takes a list of ILoggers as a constructor parameter, and forwards incoming calls to all ILoggers
    /// </summary>
    internal sealed class Log : ILog
    {
        private IEnumerable<ILogger> _loggers;
        private readonly IOperationContext _operationContext;

        private readonly Lazy<ILog> _withoutTelemetry;
        private readonly Lazy<ILog> _withoutConsole;

        public LoggerType LoggerType => LoggerType.Aggregator;

        public ILog WithoutTelemetry => _withoutTelemetry.Value;

        public ILog WithoutConsole => _withoutConsole.Value;

        public IOperationContext OperationContext => _operationContext;

        public Log(IOperationContext operationContext, IEnumerable<ILogger> loggers, LoggingVerbosity loggingVerbosity = LoggingVerbosity.Verbose)
        {
            this._loggers = loggers ?? throw new ArgumentNullException(nameof(loggers));
            this._operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));

            this._withoutTelemetry = new Lazy<ILog>(() => CreateFilteredLog(LoggerType.Telemetry, loggingVerbosity));
            this._withoutConsole = new Lazy<ILog>(() => CreateFilteredLog(LoggerType.Console, loggingVerbosity));
        }

        public void Critical(string format, params object[] args)
            => _Call(x => x.Trace(EventLevel.Critical, format, args));

        public void Error(string format, params object[] args)
            => _Call(x => x.Trace(EventLevel.Error, format, args));

        public void Info(string format, params object[] args)
            => _Call(x => x.Trace(EventLevel.Informational, format, args));

        public void Verbose(string format, params object[] args)
            => _Call(x => x.Trace(EventLevel.Verbose, format, args));

        public void Warning(string format, params object[] args)
            => _Call(x => x.Trace(EventLevel.Warning, format, args));

        public void Trace(EventLevel level, string format, params object[] args)
            => _Call(x => x.Trace(level, format, args));

        public void Exception(Exception e, bool handled = true)
            => _Call(x => x.Exception(e, handled));

        public void ExceptionAsWarning(Exception e)
            => _Call(x => x.ExceptionAsWarning(e));

        public void Request(HttpMethod httpMethod, Uri requestUri, long contentLength, PII requestBody, EventLevel eventLevel = EventLevel.Informational)
            => _Call(x => x.Request(httpMethod, requestUri, contentLength, requestBody, eventLevel));

        public void Response(HttpMethod httpMethod, Uri requestUri, MediaTypeHeaderValue contentType, long contentLength, long durationInMilliseconds, HttpStatusCode statusCode, PII responseBody, EventLevel eventLevel = EventLevel.Informational)
            => _Call(x => x.Response(httpMethod, requestUri, contentType, contentLength, durationInMilliseconds, statusCode, responseBody, eventLevel));

        public void Event(string eventName, IDictionary<string, object> properties = null, IDictionary<string, double> metrics = null, EventLevel eventLevel = EventLevel.Informational)
            => _Call(x => x.Event(eventName, properties, metrics, eventLevel));

        public void Dependency(string name, string target, bool success, TimeSpan? duration = null, IDictionary<string, object> properties = null)
            => _Call(x => x.Dependency(name, target, success, duration, properties));

        public IPerformanceLogger StartPerformanceLogger(string areaName, string metricName, IDictionary<string, object> properties = null, EventLevel eventLevel = EventLevel.Informational)
            => new PerformanceLogger(areaName, metricName, this, eventLevel, properties: properties);

        public void RemoveLoggerType(LoggerType loggerType)
        {
            this._loggers = this._loggers.Where(l => l.LoggerType != loggerType);
        }

        public void Flush(TimeSpan timeout)
        {
            try
            {
                // Instead of .Wait(timeout), Use this combination of .RunSync() and .WhenAny() to avoid deadlocking
                AsyncHelpers.RunSync(() => System.Threading.Tasks.Task.WhenAny(this.FlushAsync(), System.Threading.Tasks.Task.Delay(timeout)));
            }
            catch { }
        }

        public System.Threading.Tasks.Task FlushAsync()
        {
            var flushTasks = new List<System.Threading.Tasks.Task>();
            _Call(x => flushTasks.Add(x.FlushAsync()));

            return System.Threading.Tasks.Task.WhenAll(flushTasks.ToArray());
        }

        private void _Call(Action<ILogger> action)
            => this._loggers.ExecuteForEach(x => action(x));

        private Log CreateFilteredLog(LoggerType type, LoggingVerbosity loggingVerbosity)
        {
            if (_loggers.Any(l => l.LoggerType == type))
            {
                var newLog = new Log(_operationContext, _loggers, loggingVerbosity);
                newLog.RemoveLoggerType(type);
                return newLog;
            }
            return this;
        }
    }
}