// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.EndpointManagerLauncher
{
    public static class Program
    {

        public static int Main(string[] args)
        {
            if (args.Length != 6)
            {
                throw new ArgumentException($"Received {args.Length} args. Expected 6 args: DOTNET_ROOT, endpointManagerLaunchPath, username, socketFilePath, logFileDirectory and correlationId.");
            }

            var logFileWriter = GetLogFileWriter(args[4]);

            var endpointManagerQuotedArguments = $"\"{args[2]}\" \"{args[3]}\" \"{args[4]}\" \"{args[5]}\"";
            var endpointManagerLaunchPath = args[1];
            var dotNetRoot = args[0];

            logFileWriter.WriteLineAsync($"Launching '{endpointManagerLaunchPath}' with arguments '{endpointManagerQuotedArguments}' using '{Constants.EndpointManager.LauncherProcessName}'").ConfigureAwait(false);

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = endpointManagerLaunchPath,
                    Arguments = endpointManagerQuotedArguments,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden,
                };

                psi.EnvironmentVariables["DOTNET_ROOT"] = dotNetRoot;
                logFileWriter.WriteLineAsync($"Set DOTNET_ROOT environment variable with value: '{dotNetRoot}'").ConfigureAwait(false);

                Process.Start(psi);
                logFileWriter.WriteLineAsync($"Launched {Constants.EndpointManager.ProcessName}.");
                return 0;
            }
            catch (Exception ex)
            {
                logFileWriter.WriteLineAsync($"Failed launching '{Constants.EndpointManager.ProcessName}' using '{Constants.EndpointManager.LauncherProcessName}' with error: {ex}").ConfigureAwait(false);
                return 1;
            }
            finally
            {
                logFileWriter.FlushAsync().ConfigureAwait(false);
                logFileWriter.Dispose();
                if (Debugger.IsAttached)
                {
                    // End
                    Debugger.Break();
                }
            }
        }

        private static IThreadSafeFileWriter GetLogFileWriter(string logFileDirectory)
        {
            string logFileName = $"{Constants.Product.NameAbbreviation.ToLowerInvariant()}-{Constants.EndpointManager.LauncherProcessName.ToLowerInvariant()}-{DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss")}-{Process.GetCurrentProcess().Id}.txt";
            var logFilePath = Path.Combine(logFileDirectory, logFileName);
            return new ThreadSafeFileWriter(logFilePath);
        }
    }
}
