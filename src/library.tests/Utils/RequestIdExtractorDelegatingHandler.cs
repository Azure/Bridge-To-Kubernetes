// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Library.Tests.Utils
{
    public class RequestIdExtractorDelegatingHandler : DelegatingHandler
    {
        public IList<string> RequestIds { get; private set; } = new List<string>();

        public IList<string> ClientRequestIds { get; private set; } = new List<string>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            ClientRequestIds.Add(request?.GetClientRequestId());
            RequestIds.Add(response?.GetRequestId());
            return response;
        }
    }
}