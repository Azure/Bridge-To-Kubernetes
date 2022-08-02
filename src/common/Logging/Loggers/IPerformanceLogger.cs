// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal interface IPerformanceLogger : IDisposable
    {
        /// <summary>
        /// Amount of time that has elapsed
        /// </summary>
        TimeSpan Elapsed { get; }

        void SetResult(OperationResult result, OperationResultDetails? details = null);

        /// <summary>
        /// Adds/sets a property
        /// </summary>
        void SetProperty(string key, object value);

        /// <summary>
        /// Sets the OperationResult to Succeeded
        /// </summary>
        void SetSucceeded(OperationResultDetails? details = null);

        /// <summary>
        /// Sets the OperationResult to Cancelled
        /// </summary>
        void SetCancelled(OperationResultDetails? details = null);

        /// <summary>
        /// Sets the OperationResult to BadRequest
        /// </summary>
        /// <param name="details"></param>
        void SetBadRequest(OperationResultDetails? details = null);
    }
}