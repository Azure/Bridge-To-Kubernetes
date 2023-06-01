// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics.Tracing;
using Microsoft.BridgeToKubernetes.Common.Json;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Base class for ILogger implementations using text as output (File, Console, etc.)
    /// </summary>
    internal abstract class TextLoggerBase : LoggerBase
    {
        /// <summary>
        /// Unique identifier identifying the source of logs. This will be logged with any log.
        /// </summary>
        private readonly string _sourceIdentifier;

        /// <summary>
        /// Stores the value of the latest operation context we logged. That way, we can skip logging
        /// it if nothing changed and avoid spamming the logs.
        /// </summary>
        private string _previousOperationContextJson;

        public TextLoggerBase(IOperationContext context, string sourceIdentifier)
            : base(context)
        {
            this._sourceIdentifier = sourceIdentifier;
            this._previousOperationContextJson = null;
        }

        protected string FormatLog(EventLevel level, string message)
        {
            message = this.AppendOperationContextIfNeeded(message);

            // Replace all line jumps by their text equivalent to avoid breaking the text formatting.
            message = message.Replace(Environment.NewLine, @"\n");

            return base.FormatMessage($"{DateTime.UtcNow.ToString("o")} | {this._sourceIdentifier} | {GetLevelText(level)} | {message}");
        }

        protected string GetJsonLogMessage(string identifier, object obj)
        {
            var json = JsonHelpers.SerializeForLoggingPurpose(obj);
            return GetJsonLogMessage(identifier, json);
        }

        private string GetJsonLogMessage(string identifier, string json)
        {
            return $"{identifier} <json>{json}</json>";
        }

        private string AppendOperationContextIfNeeded(string message)
        {
            var operationContextJson = JsonHelpers.SerializeForLoggingPurpose(OperationContext);
            // Only appends the OperationContext if it has changed compared to the latest time it was logged.
            if (operationContextJson != this._previousOperationContextJson)
            {
                // A property changed in the operationContext. Let's display it again.
                message += GetJsonLogMessage($"{Environment.NewLine}Operation context:", operationContextJson);
                this._previousOperationContextJson = operationContextJson;
            }
            return message;
        }

        private string GetLevelText(EventLevel level)
        {
            switch (level)
            {
                case EventLevel.Critical:
                case EventLevel.Error:
                    return "ERROR";

                case EventLevel.Warning:
                    return "WARNG";

                case EventLevel.Informational:
                case EventLevel.Verbose:
                case EventLevel.LogAlways:
                default:
                    return "TRACE";
            }
        }
    }
}