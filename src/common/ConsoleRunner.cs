// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using Autofac;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using static Microsoft.BridgeToKubernetes.Common.RunnerCommon;

namespace Microsoft.BridgeToKubernetes.Common
{
    internal static class ConsoleRunner
    {
        /// <summary>
        /// Create and run the provided console app type out of the provided container, ensuring graceful shutdown when terminated by Docker, with log flushing
        /// </summary>
        public static int RunApp<T>(
            Func<IContainer> containerFunc,
            string[] args,
            string userAgent,
            int logFlushTimeoutMs = 60 * 1000,
            bool suppressConsoleErrorLogs = false) where T : AppBase
        {
            SecurityRequirements.Set();
            IContainer container = null;
            ILog log = null;
            var cts = new CancellationTokenSource();
            var shutdownWaitHandle = new ManualResetEventSlim(false);

            try
            {
                container = containerFunc.Invoke();
                log = container.Resolve<ILog>();

                SetupAppDomainHandlers(log, typeof(T).Assembly, () => cts.Cancel(), shutdownWaitHandle);
                InitializeOperationContext(container.Resolve<IOperationContext>(), userAgent);

                T app = container.Resolve<T>();
                return app.Execute(args, cts.Token);
            }
            catch (DependencyResolutionException e)
            {
                if (!suppressConsoleErrorLogs)
                {
                    Console.Error.WriteLine($"DependencyResolutionException: {e.GetInnermostException().Message}");
                }
                log?.WithoutConsole.Exception(e);
                return 1;
            }
            catch (Exception e)
            {
                if (!suppressConsoleErrorLogs)
                {
                    Console.Error.WriteLine($"Encountered exception: {e.ToString()}");
                }
                log?.WithoutConsole.Exception(e);
                return 1;
            }
            finally
            {
                container?.Resolve<ILog>().Flush(TimeSpan.FromMilliseconds(logFlushTimeoutMs));
                container?.Dispose();

                shutdownWaitHandle.Set();

                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }
    }
}