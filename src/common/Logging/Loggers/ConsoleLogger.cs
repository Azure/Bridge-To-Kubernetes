// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// ILogger implementation that writes to Console.Error or Console.Out depending on EventLevel
    /// </summary>
    internal class ConsoleLogger : TextLoggerBase
    {
        protected override LoggingVerbosity LoggingVerbosity { get; }
        public override LoggerType LoggerType => LoggerType.Console;

        public ConsoleLogger(IOperationContext context, IConsoleLoggerConfig consoleLoggerConfig)
            : base(context, sourceIdentifier: consoleLoggerConfig.ApplicationName)
        {
            this.LoggingVerbosity = consoleLoggerConfig.ConsoleLoggingVerbosity;
        }

        public override Task FlushAsync()
            => Task.WhenAll(Console.Out.FlushAsync(), Console.Error.FlushAsync());

        protected override void Write(EventLevel level, string line)
        {
            TextWriter writer = null;
            switch (level)
            {
                case EventLevel.Critical:
                case EventLevel.Error:
                    writer = Console.Error;
                    break;

                default:
                    writer = Console.Out;
                    break;
            }

            try
            {
                line = base.FormatLog(level, line);
                writer.WriteLine(line);
            }
            catch { }
        }

        protected override void EventInner(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            try
            {
                TextWriter writer = Console.Out;

                var eventObj = new
                {
                    EventName = eventName,
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
                TextWriter writer = Console.Out;

                var dependencyObj = new
                {
                    Name = name,
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