// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Task = System.Threading.Tasks.Task;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Common helper methods for Mindaro service runners
    /// </summary>
    internal static class RunnerCommon
    {
        public static void InitializeOperationContext(IOperationContext context, string userAgent)
        {
            context.UserAgent = userAgent;

            if (string.IsNullOrWhiteSpace(context.CorrelationId))
            {
                context.CorrelationId = Guid.NewGuid().ToString();
            }
            if (string.IsNullOrWhiteSpace(context.ClientRequestId))
            {
                context.ClientRequestId = Guid.NewGuid().ToString();
            }
        }

        public static void SetupAppDomainHandlers(ILog log, Assembly assemblyToWatch, Action processExitCallback, ManualResetEventSlim shutdownWaitHandle)
        {
            AppDomain.CurrentDomain.UnhandledException += (x, exArgs) =>
            {
                try
                {
                    Exception ex = (Exception)exArgs.ExceptionObject;
                    log.Exception(ex);
                    log.Critical("Unhandled AppDomain exception! {0}: {1}", ex.GetType().ToString(), ex.Message);
                    log.Critical("AppDomain base exception details: {0}: {1}", ex.GetBaseException()?.GetType().ToString(), ex.GetBaseException()?.Message);
                    log.Critical("Is AppDomain terminating? '{0}'", exArgs.IsTerminating.ToString());
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
                finally
                {
                    try { log?.Flush(TimeSpan.FromMinutes(1)); } catch { }
                }
            };
            AppDomain.CurrentDomain.DomainUnload += (x, y) =>
            {
                try
                {
                    log.Info("AppDomain unloading");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
                finally
                {
                    try { log?.Flush(TimeSpan.FromMinutes(1)); } catch { }
                }
            };
            int processExitCallbackCalled = 0;
            Console.CancelKeyPress += (x, y) =>
            {
                // This handler gets called when Ctrl+C is pressed
                // Cancel default handling so we have an opportunity to clean up
                y.Cancel = true;
                Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(t => Environment.Exit((int)Constants.ExitCode.ForceTerminate)).Forget();
                try
                {
                    log.Info("Cancel key pressed. Exiting...");
                    if (Interlocked.Exchange(ref processExitCallbackCalled, 1) == 0)
                    {
                        processExitCallback();
                    }
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
            };
            AppDomain.CurrentDomain.ProcessExit += (x, y) =>
            {
                // This handler gets called when the runtime intercepts the SIGTERM signal from Docker.
                // To shutdown gracefully, we must manually tell the webhost to shut down.
                try
                {
                    log.Info("Process exiting...");
                    if (Interlocked.Exchange(ref processExitCallbackCalled, 1) == 0)
                    {
                        processExitCallback();
                    }
                    shutdownWaitHandle.Wait();
                    log.Info("Process exited");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
                finally
                {
                    try { log?.Flush(TimeSpan.FromMinutes(1)); } catch { }
                }
            };
            AssemblyLoadContext.GetLoadContext(assemblyToWatch).Unloading += (x) =>
            {
                try
                {
                    log.Info("AssemblyLoadContext unloading");
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e.ToString());
                }
                finally
                {
                    try { log?.Flush(TimeSpan.FromMinutes(1)); } catch { }
                }
            };
        }
    }
}