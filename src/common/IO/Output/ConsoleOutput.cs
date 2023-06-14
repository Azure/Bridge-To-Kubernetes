// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal class ConsoleOutput : IConsoleOutput
    {
        private readonly ILog _log;
        private readonly IConsole _console;
        private const string ActiveRowHeader = "*";
        private const string InactiveRowHeader = " ";

        public ConsoleOutput(ILog log, IConsole console, CommandLineArgumentsManager commandLineOptions)
        {
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._console = console ?? throw new ArgumentNullException(nameof(console));
            if (commandLineOptions == null)
            {
                throw new ArgumentNullException(nameof(commandLineOptions));
            }
            this.Verbosity = commandLineOptions.Verbosity;
            this.OutputFormat = commandLineOptions.OutputFormat;
        }

        /// <summary>
        /// Manage the verbosity of the console output
        /// </summary>
        /// <remarks>
        /// This property is exposed publicly for testability until we refactor the CommandLineArgumentsManager.
        /// Please do NOT take a dependency on it.
        /// </remarks>
        public LoggingVerbosity Verbosity { get; private set; }

        /// <summary>
        /// Manage the format of the console output
        /// </summary>
        /// <remarks>
        /// This property is exposed publicly for testability until we refactor the CommandLineArgumentsManager.
        /// Please do NOT take a dependency on it.
        /// </remarks>
        public OutputFormat OutputFormat { get; set; }

        public void Exception(Exception ex)
        {
            if (ex.InnerException != null)
            {
                this.Exception(ex.InnerException);
            }

            if (ex is InvalidUsageException)
            {
                this.Error(ex.Message, true);
            }
        }

        public void Error(string text, bool newLine = true)
            => this.Write(EventLevel.Error, text, newLine);

        public void Warning(string text, bool newLine = true)
            => this.Write(EventLevel.Warning, text, newLine);

        public void Info(string text, bool newLine = true)
            => this.Write(EventLevel.Informational, text, newLine);

        public void Verbose(string text, bool newLine = true)
            => this.Write(EventLevel.Verbose, text, newLine);

        public void Write(EventLevel level, string text, bool newLine = true)
        {
            if (!this.Verbosity.Includes(level))
            {
                return;
            }

            if (newLine)
            {
                text += Environment.NewLine;
            }

            _log.WithoutTelemetry.Trace(level, text);
            if (this.OutputFormat == OutputFormat.Json && level <= EventLevel.Error)
            {
                // We're supposed to be outputting only JSON or fatal errors. Write the error and then abort
                _console.WriteError(text);
                return;
            }

            if (level <= EventLevel.Warning)
            {
                _console.WriteError(text);
            }
            else
            {
                _console.Write(text);
            }
        }

        public void Data<T>(IEnumerable<T> data)
        {
            if (data is null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (this.Verbosity == LoggingVerbosity.Quiet)
            {
                return;
            }

            if (this.OutputFormat == OutputFormat.Json)
            {
                string formattedText = JsonHelpers.SerializeForLoggingPurposeIndented(data);
                _console.Write(formattedText);
                _log.WithoutTelemetry.Info($"Console output (JSON):{Environment.NewLine}{formattedText}");
            }
            else if (this.OutputFormat == OutputFormat.Table)
            {
                this.WriteTable(data);
            }
        }

        public void Flush()
        {
            _console.FlushOutput();
            _console.FlushError();
        }

        /// <summary>
        /// Output the RequestId if present, otherwise the ClientRequestId
        /// </summary>
        /// <param name="operationIds"></param>
        public void WriteRequestId(IOperationIds operationIds)
        {
            if (operationIds == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(operationIds.RequestId))
            {
                Error(string.Format(CommonResources.RequestIdErrorMessageFormat, operationIds.RequestId), true);
            }
            else if (!string.IsNullOrEmpty(operationIds.ClientRequestId))
            {
                Error(string.Format(CommonResources.ClientRequestIdErrorMessageFormat, operationIds.ClientRequestId), true);
            }
        }

        private void WriteTable<T>(IEnumerable<T> outputDataItems)
        {
            if (outputDataItems is null)
            {
                throw new ArgumentNullException(nameof(outputDataItems));
            }

            // Retrieve public properties of the data, and make sure that they are not to be ignored in Table format.
            var properties = typeof(T).GetProperties().Where(p => !Attribute.IsDefined(p, typeof(TableOutputFormatIgnore)));

            // Get the formatted data items
            var formattedDataItems = new List<List<string>>();
            bool hasSelectedElement = false;

            foreach (var item in outputDataItems)
            {
                var formattedDataItem = properties.Select(p =>
                {
                    var value = p.GetValue(item);

                    // Special case the "Selected" field
                    if (StringComparer.OrdinalIgnoreCase.Equals(p.Name, "Selected") && p.PropertyType == typeof(bool))
                    {
                        bool isSelected = (bool)value;
                        if (isSelected)
                        {
                            hasSelectedElement = true;
                            return ActiveRowHeader;
                        }
                        return InactiveRowHeader;
                    }

                    // Special case Date fields
                    if (p.PropertyType == typeof(DateTime))
                    {
                        return ((DateTime)value).FormatDateTimeUtcAsAgoString(roundToLargestUnitOfTime: true);
                    }

                    return value?.ToString() ?? string.Empty;
                }).ToList();
                formattedDataItems.Add(formattedDataItem);
            }

            // Get the table headers
            var headers = properties.Select(p =>
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(p.Name, "Selected"))
                {
                    var displayName = p.GetCustomAttributes(typeof(DisplayNameAttribute), false).SingleOrDefault();
                    if (displayName != null)
                    {
                        return ((DisplayNameAttribute)displayName).DisplayName;
                    }
                    else
                    {
                        return p.Name;
                    }
                }
                else
                {
                    // Special case for the "Selected" column
                    return hasSelectedElement ? InactiveRowHeader : ActiveRowHeader;
                }
            });

            // Calculate column widths
            var columnWidths = new List<int>();
            foreach (var header in headers)
            {
                columnWidths.Add(header.Length);
            }

            formattedDataItems.ExecuteForEach(formattedDataItem =>
            {
                for (var i = 0; i < formattedDataItem.Count; i++)
                {
                    columnWidths[i] = Math.Max(columnWidths[i], formattedDataItem[i]?.Length ?? 0);
                }
            });

            var formattedRowsToLog = new List<string>();
            formattedRowsToLog.Add(this.WriteTableRow(headers.ToList(), columnWidths));

            var headerUnderlines = columnWidths.Select(width => new string('-', width)).ToList();
            formattedRowsToLog.Add(this.WriteTableRow(headerUnderlines, columnWidths));

            foreach (var formattedDataItem in formattedDataItems)
            {
                formattedRowsToLog.Add(this.WriteTableRow(formattedDataItem, columnWidths));
            }

            var formattedOutputToLog = string.Join(Environment.NewLine, formattedRowsToLog);
            _log.WithoutTelemetry.Info($"Console output (Table):{Environment.NewLine}{formattedOutputToLog}");
        }

        /// <summary>
        /// Prints a row of values.
        /// </summary>
        /// <returns>The formatted text.</returns>
        private string WriteTableRow(List<string> values, List<int> columnWidths)
        {
            var rowBuilder = new StringBuilder();
            for (var i = 0; i < values.Count; i++)
            {
                if (columnWidths[i] > 0)
                {
                    var value = values[i] ?? string.Empty;
                    rowBuilder.Append($"{value.PadRight(columnWidths[i])}  ");
                }
            }

            var formattedText = rowBuilder.ToString().TrimEnd();
            _console.WriteLine(formattedText);
            return formattedText;
        }
    }
}