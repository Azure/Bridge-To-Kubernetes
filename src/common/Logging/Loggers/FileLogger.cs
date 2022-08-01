// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// ILogger implementation that writes to a log file
    /// </summary>
    internal class FileLogger : TextLoggerBase, IFileLogger
    {
        private readonly IThreadSafeFileWriter _logFileWriter;
        private readonly bool _logFileEnabled;

        protected override LoggingVerbosity LoggingVerbosity { get; }

        public override LoggerType LoggerType => LoggerType.File;

        public string CurrentLogDirectoryPath => !string.IsNullOrWhiteSpace(_logFileWriter.CurrentFilePath) ? Path.GetDirectoryName(_logFileWriter.CurrentFilePath) : string.Empty;

        public FileLogger(IOperationContext context, IThreadSafeFileWriter logFileWriter, IFileLoggerConfig config)
            : base(context, sourceIdentifier: config.ApplicationName)
        {
            this._logFileWriter = logFileWriter ?? throw new ArgumentNullException(nameof(logFileWriter));
            this.LoggingVerbosity = config.LogFileVerbosity;
            this._logFileEnabled = config.LogFileEnabled;
        }

        public override Task FlushAsync() => _logFileEnabled ? _logFileWriter.FlushAsync() : Task.CompletedTask;

        protected override void Write(EventLevel level, string line)
        {
            if (!_logFileEnabled)
            {
                return;
            }
            try
            {
                line = base.FormatLog(level, line);
                AsyncHelpers.RunSync(() => _logFileWriter.WriteLineAsync(line));
            }
            catch { }
        }

        protected override void EventInner(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                var eventObj = new
                {
                    Properties = properties,
                    Metrics = metrics
                };
                Write(EventLevel.Informational, base.GetJsonLogMessage($@"Event: {eventName}", eventObj));
            }
            catch { }
        }

        protected override void DependencyInner(string name, string target, bool success, TimeSpan? duration = null, IDictionary<string, string> properties = null)
        {
            try
            {
                var dependencyObj = new
                {
                    Target = target,
                    Success = success,
                    Duration = duration,
                    Properties = properties
                };
                Write(success ? EventLevel.Informational : EventLevel.Error, base.GetJsonLogMessage($@"Dependency: {name}", dependencyObj));
            }
            catch { }
        }
    }
}