// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Extensions;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// HTTP client middleware that sets the request IDs on the current <see cref="IOperationContext"/>
    /// </summary>
    internal class OperationContextRequestIdsHttpHandler : DelegatingHandler
    {
        protected readonly IOperationContext _context;

        public OperationContextRequestIdsHttpHandler(IOperationContext context)
        {
            this._context = context;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;
            try
            {
                response = await base.SendAsync(request, cancellationToken);
                _context?.SetRequestIds(request, response);
                return response;
            }
            catch (Exception)
            {
                _context?.SetRequestIds(request, response);
                throw;
            }
        }
    }
}