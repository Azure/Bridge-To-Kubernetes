// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;

namespace Microsoft.BridgeToKubernetes.Library.Tests.Utils
{
    public class FakeHttpClientHandler : HttpClientHandler
    {
        public FakeHttpClientHandler()
        {
            Response = A.Fake<HttpResponseMessage>();
            Response.StatusCode = HttpStatusCode.OK;
        }

        /// <summary>
        /// Must be either HttpResponseMessage or Exception
        /// </summary>
        public virtual HttpResponseMessage Response { get; set; }

        protected override sealed Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Response);
        }
    }
}