// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Autofac;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.Extensions.Hosting;
using static Microsoft.BridgeToKubernetes.Common.RunnerCommon;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Utilities for AspNetCore apps
    /// </summary>
    internal static class AspNetCoreRunner
    {
        /// <summary>
        /// Create and run an IHost, ensuring graceful shutdown when terminated by Docker, with log flushing
        /// </summary>
        public static void RunHost(Assembly webhostAssembly, Func<string[], IHost> hostBuilder, string[] args, string userAgent)
        {
            SecurityRequirements.Set();
            var shutdownWaitHandle = new ManualResetEventSlim(false);
            try
            {
                using (var webHost = hostBuilder.Invoke(args))
                using ((ILifetimeScope)webHost.Services.GetService(typeof(ILifetimeScope)))  // Ensure root lifetime scope, and all the services in it, gets disposed
                {
                    var log = (ILog)webHost.Services.GetService(typeof(ILog));
                    var operationContext = (IOperationContext)webHost.Services.GetService(typeof(IOperationContext));

                    SetupAppDomainHandlers(log, webhostAssembly, () => webHost.StopAsync().GetAwaiter().GetResult(), shutdownWaitHandle);
                    InitializeOperationContext(operationContext, userAgent);

                    try
                    {
                        webHost.Run();
                        log.Info("Shutting down gracefully");
                    }
                    catch (Exception e)
                    {
                        log.Exception(e);
                        log.Critical("Shutting down from exception! {0}: {1}", e.GetType().ToString(), e.Message);
                        log.Critical("Base exception details: {0}: {1}", e.GetBaseException()?.GetType().ToString(), e.GetBaseException()?.Message);
                    }
                    finally
                    {
                        log.Flush(TimeSpan.FromMinutes(1));
                    }
                }
            }
            catch (DependencyResolutionException e)
            {
                Console.Error.WriteLine(e.GetInnermostException().Message);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e.ToString());
            }

            // Set ready to shutdown
            shutdownWaitHandle.Set();

            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }
    }
}