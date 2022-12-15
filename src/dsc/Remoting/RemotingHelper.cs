// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Exe.Remoting
{
    /// <summary>
    /// This class starts a remoting server that allows callers to stop the current connect session by calling http://localhost:<port>/api/remoting/stop,
    /// which calls <see cref="Stop"/> to cancel the <see cref="_appCancellationTokenSource"/>.
    /// </summary>
    internal static class RemotingHelper
    {
        private static CancellationTokenSource _appCancellationTokenSource;
        private static ILog _log;
        private static IWebHost _host;

        public static void StartRemotingServer(
            Func<IWebHostBuilder> webHostBuilderFactory,
            int port,
            ILog log,
            CancellationTokenSource appCancellationTokenSource)
        {
            _appCancellationTokenSource = appCancellationTokenSource;
            _log = log;
            _host = webHostBuilderFactory.Invoke()
                        .UseKestrel()
                        .UseStartup<Startup>()
                        .UseUrls($"http://localhost:{port}")
                        .Build();
            _host.StartAsync(_appCancellationTokenSource.Token).Forget();
            log.Verbose($"Remoting started listening on {port}");
        }

        public static void Stop()
        {
            try
            {
                _appCancellationTokenSource?.CancelAfter(1500);
            }
            catch (Exception e)
            {
                _log?.Exception(e);
                throw;
            }
        }
    }
}