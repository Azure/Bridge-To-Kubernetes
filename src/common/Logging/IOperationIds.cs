// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal interface IOperationIds
    {
        /// <summary>
        /// The x-ms-request-id header of the response received
        /// </summary>
        string RequestId { get; }

        /// <summary>
        /// The x-ms-client-request-id header of the response received
        /// </summary>
        string ClientRequestId { get; }

        /// <summary>
        /// The x-ms-correlation-request-id header of the response received
        /// </summary>
        string CorrelationRequestId { get; }
    }
}