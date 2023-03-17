// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Interface intended for exceptions that indicates it contains an HTTP request and response
    /// </summary>
    internal interface IRequestResponse
    {
        /// <summary>
        /// The Http request
        /// </summary>
        HttpRequestMessageWrapper Request { get; }

        /// <summary>
        /// The Http response
        /// </summary>
        HttpResponseMessageWrapper Response { get; }
    }
}