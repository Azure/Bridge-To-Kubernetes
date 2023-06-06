// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Authentication;
using k8s.Autorest;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Library.ServiceClients.Handlers
{
    /// <summary>
    /// Given an ServiceClientCredentialProvider makes sure that all the outgoing requests contain the ServiceClientCredentials Authorization header.
    /// If using directly with an HttpClient, make sure you set the InnerHandler to a <see cref="HttpClientHandler"/>.
    /// </summary>
    /// <remarks>
    /// New credentials are pulled from the ServiceClientCredentialsProvider for every reqeust
    /// </remarks>

    internal class ServiceClientCredentialsHttpHandler<T> : DelegatingHandler where T : ServiceClientCredentials
    {
        private Func<Task<T>> _serviceClientCredentialsProvider;

        public ServiceClientCredentialsHttpHandler(Func<Task<T>> serviceClientCredentialsProvider)
        {
            this._serviceClientCredentialsProvider = serviceClientCredentialsProvider ?? throw new ArgumentNullException(nameof(serviceClientCredentialsProvider));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cred = await _serviceClientCredentialsProvider();
            await cred.ProcessHttpRequestAsync(request, cancellationToken);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}