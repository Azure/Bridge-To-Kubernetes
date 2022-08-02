// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net.Http;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.Rest;
using Microsoft.Rest.Azure;

namespace Microsoft.BridgeToKubernetes.Common.Extensions
{
    internal static class IOperationContextExtensions
    {
        public static void SetRequestIds(this IOperationContext context, CloudException ex)
        {
            context.RequestId = ex?.RequestId;
            context.ClientRequestId = ex?.Request?.GetClientRequestId();
            context.CorrelationId = ex?.Response?.GetCorrelationRequestId();
        }

        public static void SetRequestIds(this IOperationContext context, HttpRequestMessage request, HttpResponseMessage response)
        {
            context.RequestId = response?.GetRequestId();
            context.ClientRequestId = request?.GetClientRequestId();
            context.CorrelationId = response?.GetCorrelationRequestId();
        }

        public static void SetRequestIds(this IOperationContext context, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response)
        {
            context.RequestId = response?.GetRequestId();
            context.ClientRequestId = request?.GetClientRequestId();
            context.CorrelationId = response?.GetCorrelationRequestId();
        }
    }
}