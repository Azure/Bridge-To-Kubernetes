// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.BridgeToKubernetes.Common.Logging;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal class ConsoleLauncher : IConsoleLauncher
    {
        private readonly ILog _log;
        private IPlatform _platform;
        private Process _currentConsoleProcess = null; // Limit to only one process, so if we relaunch we can re-use it

        public ConsoleLauncher(ILog log, IPlatform platform)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        }

        /// <summary>
        /// Launch a terminal window locally with desired environment variables.
        /// </summary>
        public Process LaunchTerminalWithEnv(IDictionary<string, string> envVars, string envScriptPath, bool performLaunch = true)
        {
            if (_currentConsoleProcess != null && !_currentConsoleProcess.HasExited)
            {
                // during relaunch (such as deployment changed), do not open a new console window each time.
                return _currentConsoleProcess;
            }
            if (_platform.IsWindows)
            {
                _currentConsoleProcess = this.LaunchTerminalWithEnvWindows(envVars, envScriptPath, performLaunch);
            }
            else
            {
                _currentConsoleProcess = this.LaunchTerminalWithEnvBash(envVars, envScriptPath, performLaunch);
            }
            return _currentConsoleProcess;
        }

        private Process LaunchTerminalWithEnvWindows(IDictionary<string, string> envVars, string scriptCmd, bool performLaunch)
        {
            StringBuilder b = new StringBuilder();
            b.AppendLine($"title {Product.Name} Environment");
            foreach (var v in envVars)
            {
                b.AppendLine($"SET \"{v.Key}={v.Value}\"");
            }
            b.AppendLine("@echo ####################################################################");
            b.AppendLine("@echo Use this terminal to run your application.");
            b.AppendLine("@echo ####################################################################");

            File.WriteAllText(scriptCmd, b.ToString());
            _log.Verbose("Script {0} created.", new PII(scriptCmd));

            if (performLaunch)
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    UseShellExecute = true, // TODO: use false gives the best experience but issues with Ctrl-C, Console.ReadLine from debugged app
                    FileName = "cmd.exe",
                    Arguments = $"/K \"{scriptCmd}\""
                };
                var cmdProcess = Process.Start(psi);
                return cmdProcess;
            }
            else
            {
                return null;
            }
        }

        private Process LaunchTerminalWithEnvBash(IDictionary<string, string> envVars, string scriptCmd, bool performLaunch)
        {
            StringBuilder b = new StringBuilder();
            foreach (var v in envVars)
            {
                b.AppendLine($"export {v.Key}={v.Value}");
            }
            File.WriteAllText(scriptCmd, b.ToString());
            _log.Verbose($"Script {scriptCmd} created.");

            if (performLaunch)
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    UseShellExecute = false,
                    FileName = "/bin/bash"
                };
                foreach (var envValue in envVars)
                {
                    psi.EnvironmentVariables[envValue.Key] = envValue.Value;
                }
                return Process.Start(psi);
            }
            else
            {
                return null;
            }
        }
    }
}