// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System.Net.Http;

namespace Microsoft.BridgeToKubernetes.Common.Extensions
{
    internal static class IOperationContextExtensions
    {
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