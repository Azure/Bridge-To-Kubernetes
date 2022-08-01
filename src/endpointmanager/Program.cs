// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using Autofac;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    public static class Program
    {
        /// <summary>
        /// Main entrypoint for the application
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static int Main(string[] args)
        {
            IContainer container = null;
            try
            {
                // Entrypoint
                // Note: we validate arguments here since we require temp file path to be present before registering our logging dependencies with autofac.
                if (args.Length != 4)
                {
                    throw new ArgumentException($"Received {args.Length} args. Expected 4 args: username, socketFilePath, logFileDirectory and correlationId.");
                }
                container = AppContainerConfig.BuildContainer(args);
                container.Resolve<ILog>().Info($"Starting up {nameof(EndpointManager)}");

                var culture = container.Resolve<IEnvironmentVariables>().Culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                return container.Resolve<EndpointManager>().Execute(args, new CancellationToken());
            }
            catch (Exception e) when (e is IUserVisibleExceptionReporter)
            {
                try { container?.Resolve<ILog>().Exception(e); } catch { }
                Console.Error.WriteLine(e.Message);
                return (int)ExitCode.Fail;
            }
            catch (Exception e)
            {
                // Unexpected error
                Console.Error.WriteLine(e);
                return (int)ExitCode.Fail;
            }
            finally
            {
                if (Debugger.IsAttached)
                {
                    // End
                    Debugger.Break();
                }
                try { container?.Dispose(); } catch { }
            }
        }
    }
}