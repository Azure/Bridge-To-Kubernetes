// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Provides a standardized way to track operation length, and fire the info to telemetry systems
    /// </summary>
    internal class PerformanceLogger : IPerformanceLogger
    {
        private readonly string _areaName;
        private readonly string _operationName;
        private Stopwatch _stopwatch = new Stopwatch();
        private readonly IDictionary<Dimension, string> _dimensions = new Dictionary<Dimension, string>();
        private IDictionary<string, object> _properties = new Dictionary<string, object>();
        private EventLevel _eventLevel;
        private readonly ILog _log;

        /// <summary>
        /// Standard constructor
        /// </summary>
        public PerformanceLogger(string areaName, string operationName, ILog log, EventLevel eventLevel, IDictionary<string, object> properties = null)
        {
            this._areaName = areaName;
            this._operationName = operationName;
            this._eventLevel = eventLevel;
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            if (properties != null)
            {
                this._properties = properties;
            }
            // By default we set the default OperationResult as failed so we have to manually set it to Successfull at the end of every happy path.
            // It is better to have false failures than false successes.
            this.SetResult(OperationResult.Failed);
            this._stopwatch.Start();
        }

        /// <summary>
        /// <see cref="IPerformanceLogger.Elapsed"/>
        /// </summary>
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        /// <summary>
        /// <see cref="IPerformanceLogger.SetProperty(string, object)"/>
        /// </summary>
        public void SetProperty(string key, object value)
        {
            this._properties[key] = value;
        }

        public void SetResult(OperationResult result, OperationResultDetails? details = null)
        {
            this.SetDimension(Dimension.Result, result.ToString());
            if (details.HasValue)
            {
                this.SetDimension(Dimension.ResultDetail, details.Value.ToString());
            }
        }

        /// <summary>
        /// <see cref="IPerformanceLogger.SetSucceeded"/>
        /// </summary>
        public void SetSucceeded(OperationResultDetails? details = null)
        {
            this.SetResult(OperationResult.Succeeded, details);
        }

        /// <summary>
        /// <see cref="IPerformanceLogger.SetCancelled"/>
        /// </summary>
        public void SetCancelled(OperationResultDetails? details = null)
        {
            this.SetResult(OperationResult.Cancelled, details);
        }

        /// <summary>
        /// <see cref="IPerformanceLogger.SetBadRequest"/>
        /// </summary>
        public void SetBadRequest(OperationResultDetails? details = null)
        {
            this.SetResult(OperationResult.BadRequest, details);
        }

        /// <summary>
        /// Stops the stopwatch and fires the events
        /// </summary>
        public void Dispose()
        {
            _stopwatch.Stop();
            var properties = this._properties
                     .Concat(this._dimensions.Select(x => new KeyValuePair<string, object>(x.Key.ToString(), x.Value)))
                     .ToDictionary(x => x.Key, x => x.Value);

            _log.Event(
                $"{_areaName}-{_operationName}",
                properties: properties,
                metrics: new Dictionary<string, double> { { "DurationInMs", _stopwatch.ElapsedMilliseconds } },
                eventLevel: _eventLevel);
        }

        private void SetDimension(Dimension key, string value)
        {
            this._dimensions[key] = value;
        }

        /// <summary>
        /// Enum for Dimensions (string values for grouping metrics)
        /// </summary>
        private enum Dimension
        {
            Result,
            ResultDetail
        }
    }
}