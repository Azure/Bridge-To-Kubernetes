// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests.Extenions
{
    public static class Extensions
    {
        public static void AddRequestId(this HttpResponseMessage msg)
        {
            msg.Headers.Add(Common.Constants.CustomHeaderNames.RequestId, Guid.NewGuid().ToString());
        }

        internal static void VerifyRequestsIdsMatch(this IOperationIds result, IOperationContext context)
        {
            Assert.False(string.IsNullOrEmpty(context.RequestId));
            Assert.False(string.IsNullOrEmpty(context.ClientRequestId));
            Assert.Equal(context.RequestId, result.RequestId);
            Assert.Equal(context.ClientRequestId, result.ClientRequestId);
        }

        internal static void VerifyRequestsIdsMatchForRequestResponse(this IRequestResponse result, IOperationContext context)
        {
            Assert.NotNull(result.Request);
            Assert.NotNull(result.Response);
            Assert.False(string.IsNullOrEmpty(context.RequestId));
            Assert.False(string.IsNullOrEmpty(context.ClientRequestId));
            Assert.Equal(result.Response.GetRequestId(), context.RequestId);
            Assert.Equal(result.Request.GetClientRequestId(), context.ClientRequestId);
        }
    }
}