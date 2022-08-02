// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading;
using Autofac;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe
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
            SecurityRequirements.Set();
            IContainer container = null;
            try
            {
                // To Debug the CLI from VS Code, uncomment the below line and F5 Mindaro-Connect/src/vscode.
                // Debugger.Launch();

                // Entrypoint
                container = AppContainerConfig.BuildContainer(args);

                var culture = container.Resolve<IEnvironmentVariables>().Culture;
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                return container.Resolve<CliApp>().Execute(args, new CancellationToken());
            }
            catch (DependencyResolutionException e) when (e.GetInnermostException() is IUserVisibleExceptionReporter inner)
            {
                // Invalid usage
                Console.Error.WriteLine(inner.Message);
                return (int)ExitCode.Fail;
            }
            catch (Exception e) when (e is IUserVisibleExceptionReporter && container == null)
            {
                // Invalid usage during container build, error was likely not printed to console
                Console.Error.WriteLine(e.Message);
                return (int)ExitCode.Fail;
            }
            catch (Exception e)
            {
                // An unexpected error occurred. We display the message and some diagnostic information.
                try { container?.Resolve<ILog>().Exception(e); } catch { }
                string logFilePath = null;
                try
                {
                    logFilePath = container?.Resolve<IFileLogger>().CurrentLogDirectoryPath;
                }
                catch { }

                Console.Error.WriteLine(!string.IsNullOrEmpty(e.Message) ? string.Format(CommonResources.Error_Unexpected, e.Message) : CommonResources.Error_OopsMessage);
                Console.Error.WriteLine(string.Format(CommonResources.Error_BridgeReport, CliConstants.BridgeReportLink));
                if (!string.IsNullOrWhiteSpace(logFilePath))
                {
                    Console.Error.WriteLine(string.Format(CommonResources.Error_SeeLogFile, logFilePath));
                }

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