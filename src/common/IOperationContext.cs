// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Tracks request-level metadata
    /// </summary>
    public interface IOperationContext
    {
        /// <summary>
        /// Client request Id
        /// </summary>
        string ClientRequestId { get; set; }

        /// <summary>
        /// CorrelationId composed of the concatenation of various correlationIds generated through the course of the operation.
        /// </summary>
        string CorrelationId { get; set; }

        /// <summary>
        /// Request id
        /// </summary>
        string RequestId { get; set; }

        /// <summary>
        /// User subscription id
        /// </summary>
        string UserSubscriptionId { get; set; }

        /// <summary>
        /// Start time
        /// </summary>
        DateTimeOffset StartTime { get; set; }

        /// <summary>
        /// User agent
        /// </summary>
        string UserAgent { get; set; }

        /// <summary>
        /// Request http method
        /// </summary>
        HttpMethod RequestHttpMethod { get; set; }

        /// <summary>
        /// Request uri
        /// </summary>
        Uri RequestUri { get; set; }

        /// <summary>
        /// The version of the assembly
        /// </summary>
        string Version { get; set; }

        /// <summary>
        /// Request headers
        /// </summary>
        IDictionary<string, IEnumerable<string>> RequestHeaders { get; set; }

        /// <summary>
        /// Additional properties to be added to the logging context
        /// </summary>
        IDictionary<string, object> LoggingProperties { get; }

        /// <summary>
        /// Copies all context properties from another context into this instance
        /// </summary>
        void Inherit(IOperationContext context);
    }
}