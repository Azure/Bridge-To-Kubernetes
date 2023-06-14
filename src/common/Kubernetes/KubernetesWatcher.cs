// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Authentication;
using k8s.Autorest;
using k8s.Exceptions;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Services.Kubernetes
{
    /// <summary>
    /// Represents a client for watching Kubernetes objects.
    /// </summary>
    internal class KubernetesWatcher : IKubernetesWatcher
    {
        private const int WatchIntervalMilliseconds = 60000;
        private readonly Uri _baseUri;
        private readonly X509Certificate2Collection _caCerts;
        private readonly bool _skipTlsVerify;
        private readonly ServiceClientCredentials _credentials;
        private readonly ILog _log;
        private readonly HttpClient _httpClient;

        private bool _isDisposed = false;

        private volatile CancellationTokenSource _cts = new CancellationTokenSource();
        private volatile int _failureBackoff = 0;

        public delegate IKubernetesWatcher InClusterFactory(bool useInClusterConfig = true);

        public KubernetesWatcher(ILog log, KubernetesClientConfiguration config = null, bool useInClusterConfig = false)
        {
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            if (config == null && useInClusterConfig)
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }

            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                this._baseUri = new Uri(config.Host);
            }
            catch (UriFormatException e)
            {
                throw new KubeConfigException("Bad host url", e);
            }

            this._caCerts = config.SslCaCerts;
            this._skipTlsVerify = config.SkipTlsVerify;

            var httpClientHandler = new HttpClientHandler();

            if (_baseUri.Scheme == "https")
            {
                if (config.SkipTlsVerify)
                {
                    httpClientHandler.ServerCertificateCustomValidationCallback =
                        (sender, certificate, chain, sslPolicyErrors) => true;
                }
                else
                {
                    if (_caCerts == null)
                    {
                        throw new KubeConfigException("A CA must be set when SkipTlsVerify === false");
                    }
                    httpClientHandler.ServerCertificateCustomValidationCallback =
                        (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            return CertificateValidationCallBack(sender, _caCerts, certificate, chain,
                                sslPolicyErrors);
                        };
                }
            }

            // set credentails for the kubernetes client
            if (config.TokenProvider != null)
            {
                CancellationToken cancellationToken = new CancellationTokenSource().Token;
                AuthenticationHeaderValue credentials = config.TokenProvider.GetAuthenticationHeaderAsync(cancellationToken).Result;
                _credentials = new TokenCredentials(credentials.Parameter, credentials.Scheme);
            }
            else if (!string.IsNullOrEmpty(config.AccessToken))
            {
                _credentials = new TokenCredentials(config.AccessToken);
            }
            else if (!string.IsNullOrEmpty(config.Username))
            {
                _credentials = new BasicAuthenticationCredentials() { UserName = config.Username, Password = config.Password };
            }

            var clientCert = ClientCertUtil.GetClientCert(config);
            if (clientCert != null) 
            {
                httpClientHandler.ClientCertificates.Add(clientCert);
            }

            _httpClient = new HttpClient(httpClientHandler);
        }

        public void Dispose()
        {
            if (this._isDisposed)
            {
                this._log?.Critical("{0} is already disposed!", nameof(KubernetesWatcher));
                return;
            }

            var cts = this._cts;
            this._cts = null;
            cts.Cancel();
            cts.Dispose();
            this._isDisposed = true;
        }

        /// <summary>
        /// <see cref="IKubernetesWatcher.WatchNamespacesAsync"/>
        /// </summary>
        public Task WatchNamespacesAsync(
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken)
            => WatchAsync($"/api/v1/namespaces", callback, cancellationToken);

        /// <summary>
        /// <see cref="IKubernetesWatcher.WatchServicesAsync"/>
        /// </summary>
        public Task WatchServicesAsync(
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken)
            => WatchAsync($"api/v1/services", callback, cancellationToken);

        /// <summary>
        /// <see cref="IKubernetesWatcher.WatchIngressesAsync"/>
        /// </summary>
        public Task WatchIngressesAsync(
            string namespaceName,
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken)
            => WatchAsync($"/apis/networking.k8s.io/v1/namespaces/{namespaceName}/ingresses", callback, cancellationToken);

        /// <summary>
        /// <see cref="IKubernetesWatcher.WatchPodsAsync"/>
        /// </summary>
        public Task WatchPodsAsync(
            string namespaceName,
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken)
            => WatchAsync($"/api/v1/namespaces/{namespaceName}/pods", callback, cancellationToken);

        private Task WatchAsync(
            string path,
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken)
        {
            if (this._cts == null)
            {
                throw new ObjectDisposedException(nameof(KubernetesWatcher));
            }
            var instanceToken = this._cts.Token;

            var requestUri = new Uri(this._baseUri, $"{path}?watch=true");
            HttpResponseMessage response = null;

            return Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested && !instanceToken.IsCancellationRequested)
                {
                    using (var myCts = new CancellationTokenSource())
                    using (instanceToken.Register(() => myCts.Cancel()))
                    using (cancellationToken.Register(() => myCts.Cancel()))
                    {
                        bool backoff = false;
                        try
                        {
                            if (response == null)
                            {
                                response = await StartAsync(requestUri, myCts.Token);
                                Interlocked.Exchange(ref this._failureBackoff, 0); // reset backoff
                            }

                            await ProcessAsync(response, callback, myCts.Token);
                        }
                        catch (HttpRequestException ex)
                        {
                            // User cluster cannot be reached
                            _log.ExceptionAsWarning(ex);
                            backoff = true;
                        }
                        catch (Exception ex)
                        {
                            // Write exception for diagnostic purposes
                            _log.Exception(ex);
                            backoff = true;
                        }

                        response = null;

                        if (backoff)
                        {
                            // Wait for a period of time before trying again
                            int waitMs = Math.Min(300 * Interlocked.Increment(ref this._failureBackoff), 10000);
                            await Task.Delay(waitMs, myCts.Token);
                        }

                        myCts.Cancel();
                    }
                }
            });
        }

        private async Task<HttpResponseMessage> StartAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, requestUri))
            {
                if (this._credentials != null)
                {
                    await this._credentials.ProcessHttpRequestAsync(request, cancellationToken);
                }
                var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                return response;
            }
        }

        /// <summary>
        /// Continue processing the stream until timeout or error.
        /// Takes ownership of the passed response in a using().
        /// </summary>
        private async Task ProcessAsync(
            HttpResponseMessage response,
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken)
        {
            bool restart = false;
            try
            {
                using (response)
                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    var _ = Task.Delay(WatchIntervalMilliseconds).ContinueWith(t =>
                    {
                        // The watch API against AKS has a tendency to stop working after
                        // a period of inactivity, leaving the ingress manager
                        // in a state where it no longer responds to changes.
                        // To guarantee it keeps working, we restart the request
                        // after a period of time regardless of its state. Closing
                        // the reader causes the reader.ReadLineAsync() call to
                        // throw an IOException, so we distinguish this case from
                        // other actually erroneous IOExceptions by setting a flag
                        // and checking it in the catch clause defined below.
                        restart = true;
                        reader.Close();
                    });
                    string line = null;
                    while (!cancellationToken.IsCancellationRequested &&
                        (line = await reader.ReadLineAsync()) != null)
                    {
                        var ev = JsonHelpers.DeserializeObject<WatchEvent>(line);
                        if (ev.Object.Kind == "Status")
                        {
                            break;
                        }
                        callback(ev.Type, ev.Object.Metadata);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected, non-erroneous exception
            }
            catch (IOException) when (restart)
            {
                // Expected, non-erroneous exception
            }
        }

        private bool CertificateValidationCallBack(
            object sender,
            X509Certificate2Collection caCerts,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            // If the certificate is a valid, signed certificate, return true.
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true;
            }

            // If there are errors in the certificate chain, look at each error to determine the cause.
            if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateChainErrors) != 0)
            {
                chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

                // Added our trusted certificates to the chain
                //
                chain.ChainPolicy.ExtraStore.AddRange(caCerts);

                chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;
                var isValid = chain.Build((X509Certificate2)certificate);

                var isTrusted = false;

                var rootCert = chain.ChainElements[chain.ChainElements.Count - 1].Certificate;

                // Make sure that one of our trusted certs exists in the chain provided by the server.
                //
                foreach (var cert in caCerts)
                {
                    if (rootCert.RawData.SequenceEqual(cert.RawData))
                    {
                        isTrusted = true;
                        break;
                    }
                }

                return isValid && isTrusted;
            }

            // In all other cases, return false.
            return false;
        }

        private class WatchEvent
        {
            public WatchEventType Type { get; set; }
            public KubernetesObject Object { get; set; }
        }

        private class KubernetesObject
        {
            public string ApiVersion { get; set; }
            public string Kind { get; set; }
            public V1ObjectMeta Metadata { get; set; }
        }
    }
}