// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// Retry strategies for different exceptions thrown by HttpClient
    /// </summary>
    internal class HttpClientExceptionStrategy : ServiceBase
    {
        private readonly ILog _log;

        public HttpClientExceptionStrategy(ILog log)
            : base()
        {
            this._log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public virtual async Task<T> RetryAsync<T>(Task<T> task, string failureMessage)
        {
            TimeSpan backoffInterval = TimeSpan.FromSeconds(1.0);
            const int maxTries = 3;
            for (int tries = 1; true; tries++)
            {
                try
                {
                    return await task;
                }
                catch (Exception ex)
                {
                    this._log.Warning($"{failureMessage} {ex.GetType().Name} encountered in {nameof(RetryAsync)}<>: {ex.GetInnermostException().Message}");
                    this._log.Exception(ex);

                    if (ex is HttpRequestException hre && hre.InnerException != null && hre.InnerException is WebException
                        || ex is OperationCanceledException)
                    {
                        if (tries > maxTries)
                        {
                            this._log.Info("MaxTries exceeded. Propagating Exception");
                            throw;
                        }

                        await Task.Delay(backoffInterval);
                        backoffInterval += backoffInterval;
                        continue;
                    }

                    throw;
                }
            }
        }
    }
}