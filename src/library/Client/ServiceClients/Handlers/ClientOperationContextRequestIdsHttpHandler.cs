// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Extensions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.Rest.Azure;

namespace Microsoft.BridgeToKubernetes.Library.ServiceClients.Handlers
{
    /// <summary>
    /// HTTP client middleware that sets the request IDs on the current <see cref="IOperationContext"/>
    /// Contains additional catch handlers for the Client SDK.
    /// </summary>
    internal class ClientOperationContextRequestIdsHttpHandler : OperationContextRequestIdsHttpHandler
    {
        public ClientOperationContextRequestIdsHttpHandler(IOperationContext context)
            : base(context)
        { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                return await base.SendAsync(request, cancellationToken);
            }
            catch (CloudException e)
            {
                base._context?.SetRequestIds(e.Request, e.Response);
                throw;
            }
        }
    }
}