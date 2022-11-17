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
        private static IWebHost host;

        public static void StartRemotingServer(
            Func<IWebHostBuilder> webHostBuilderFactory,
            int port,
            ILog log,
            CancellationTokenSource appCancellationTokenSource)
        {
            try
            {
                _appCancellationTokenSource = appCancellationTokenSource;
                _log = log;
                host = webHostBuilderFactory.Invoke()
                            .UseKestrel()
                            .UseStartup<Startup>()
                            .UseUrls($"http://localhost:{port}")
                            .Build();
                host.StartAsync(_appCancellationTokenSource.Token).Forget();
                log.Verbose($"Remoting started listening on {port}");
            } catch(Exception ex)
            {
                log.Verbose($"Remoting failed to start on {port} due to exception", ex.Message);
            }
            
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