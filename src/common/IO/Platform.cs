// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    /// <summary>
    /// NetStandardPlatform Platform.
    /// </summary>
    internal class Platform : IPlatform
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int waitpid(int pid, out int status, int options);

        public bool IsWindows => OperatingSystem.IsWindows();
        public bool IsOSX => OperatingSystem.IsMacOS();
        public bool IsLinux => OperatingSystem.IsLinux();

        public async Task<(int exitCode, string userName)> DetermineCurrentUserWithRetriesAsync(CancellationToken cancellationToken)
        {
            int exitCode = 0;
            string userName = string.Empty;
            await WebUtilities.RetryUntilTimeWithWaitAsync((i) =>
            {
                (exitCode, userName) = DetermineCurrentUser();
                return Task.FromResult(!string.IsNullOrWhiteSpace(userName));
            },
            maxWaitTime: TimeSpan.FromSeconds(3),
            waitInterval: TimeSpan.FromMilliseconds(100),
            cancellationToken: cancellationToken);
            return (exitCode, userName);
        }

        /// <summary>
        /// <see cref="IPlatform.Execute(string, string, Action{string}, IDictionary{string, string}, TimeSpan, CancellationToken, out string)"/>
        /// </summary>
        public int Execute(
            string executable,
            string command,
            Action<string> logCallback,
            IDictionary<string, string> envVariables,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            out string output)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var combinedOutput = new StringBuilder();
            Action<string> callback = line =>
            {
                combinedOutput.AppendLine(line);
                logCallback?.Invoke(line);
            };
            int result = Execute(executable, command, callback, callback, envVariables, timeout, cancellationToken, out string tmp1, out string tmp2);
            output = combinedOutput.ToString();
            return result;
        }

        /// <summary>
        /// <see cref="IPlatform.Execute(string, string, Action{string}, Action{string}, IDictionary{string, string}, TimeSpan, CancellationToken, out string, out string)"/>
        /// </summary>
        public int Execute(
            string executable,
            string command,
            Action<string> stdOutCallback,
            Action<string> stdErrCallback,
            IDictionary<string, string> envVariables,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            out string stdOutOutput,
            out string stdErrOutput)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = executable,
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            var process = new ProcessEx(psi);

            if (envVariables != null)
            {
                foreach (KeyValuePair<string, string> env in envVariables)
                {
                    process.StartInfo.EnvironmentVariables[env.Key] = env.Value;
                }
            }

            using (var outputWaitCountdown = new CountdownEvent(2)) // 2 for stdout and stderr
            {
                StringBuilder stdOutLines = new StringBuilder();
                StringBuilder stdErrLines = new StringBuilder();
                object stdOutLock = new object();
                object stdErrLock = new object();
                void outputHandler(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (stdOutLock)
                        {
                            stdOutLines.AppendLine(e.Data);
                        }

                        stdOutCallback?.Invoke(e.Data);
                    }
                    else
                    {
                        try { process.OutputDataReceived -= outputHandler; } catch { }
                        try
                        {
                            // Output data has finished
                            outputWaitCountdown.Signal();
                        }
                        catch (ObjectDisposedException)
                        { }
                    }
                };
                void errorHandler(object sender, DataReceivedEventArgs e)
                {
                    if (e.Data != null)
                    {
                        lock (stdErrLock)
                        {
                            stdErrLines.AppendLine(e.Data);
                        }

                        stdErrCallback?.Invoke(e.Data);
                    }
                    else
                    {
                        try { process.ErrorDataReceived -= errorHandler; } catch { }
                        try
                        {
                            // Error data has finished
                            outputWaitCountdown.Signal();
                        }
                        catch (ObjectDisposedException)
                        { }
                    }
                }
                process.OutputDataReceived += outputHandler;
                process.ErrorDataReceived += errorHandler;

                Action killProcess =
                    () =>
                    {
                        try
                        {
                            if (!process.HasExited)
                            {
                                process.Kill();
                                process.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            stdErrLines.AppendLine(e.ToString());
                        }
                    };

                int exitCode;
                using (cancellationToken.Register(killProcess))
                {
                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();

                    int timeoutMs = timeout.TotalMilliseconds > int.MaxValue ? int.MaxValue : (int)timeout.TotalMilliseconds;
                    exitCode = WaitForProcessExit(process, timeoutMs, stdOutCallback, stdErrCallback);
                }

                try
                {
                    // Wait for all output to flush
                    // The process should already be exited at this point, so don't wait too long before giving up
                    if (!outputWaitCountdown.Wait(TimeSpan.FromSeconds(5)))
                    {
                        throw new IOException("Timed out waiting for process output to flush");
                    }
                }
                finally
                {
                    lock (stdOutLock)
                    {
                        stdOutOutput = stdOutLines.ToString();
                    }

                    lock (stdErrLock)
                    {
                        stdErrOutput = stdErrLines.ToString();
                    }

                    process.Dispose();
                }
                return exitCode;
            }
        }

        public (int exitCode, string output) ExecuteAndReturnOutput(
            string command,
            string arguments,
            TimeSpan timeout,
            Action<string> stdOutCallback,
            Action<string> stdErrCallback,
            string workingDirectory = null,
            string processInput = null)
        {
            ProcessStartInfo psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                FileName = command,
                Arguments = arguments
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory))
            {
                psi.WorkingDirectory = workingDirectory;
            }
            if (!string.IsNullOrWhiteSpace(processInput))
            {
                psi.RedirectStandardInput = true;
            }

            StringBuilder sb = new StringBuilder();
            var proc = new ProcessEx(psi);
            proc.OutputDataReceived += (sender, e) => { sb.AppendLine(e.Data); };
            proc.ErrorDataReceived += (sender, e) => { sb.AppendLine(e.Data); };

            proc.Start();

            if (!string.IsNullOrWhiteSpace(processInput))
            {
                proc.StandardInput.WriteLine(processInput);
                proc.StandardInput.Close();
            }

            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();
            var exitCode = WaitForProcessExit(proc, timeoutMs: timeout.Milliseconds, stdOutCallback: stdOutCallback, stdErrCallback: stdErrCallback);

            return (exitCode, sb.ToString());
        }

        // NOTE: Do not use this method as-is on MacOS. Use WaitForExit(), and/or redirect output as well. When we do these things, the process exits when it is done. Otherwise,
        // process.WaitForExit(timeoutMs) returns true immediately, before the timeout has elapsed.
        // For more info: https://github.com/dotnet/runtime/issues/32456
        private int WaitForProcessExit(IProcessEx process, int timeoutMs, Action<string> stdOutCallback, Action<string> stdErrCallback)
        {
            stdOutCallback?.Invoke($"Waiting for process {process.Id}");
            int exitCode;
            if (!process.WaitForExit(timeoutMs))
            {
                // Process did not terminate within the timeout
                if (stdErrCallback != null)
                {
                    stdErrCallback($"Killing process. Timeout: {timeoutMs} ms reached");
                }
                exitCode = (int)ExitCode.Timeout;
                process.Kill();
            }
            else
            {
                if (stdOutCallback != null)
                {
                    stdOutCallback($"Process has exited with exit code {process.ExitCode}");
                }
                exitCode = process.ExitCode;
            }
            return exitCode;
        }

        public IProcessEx CreateProcess(ProcessStartInfo psi)
            => new ProcessEx(psi);

        public void KillProcess(int processId)
            => Process.GetProcessById(processId).Kill();

        private Version GetOSXVersion(Action<string> stdOutCallback, Action<string> stdErrCallback)
        {
            stdOutCallback?.Invoke("Getting OSX Version");
            Version osxVersion = new Version(10, 14);   // By default, assume OSX Majave 10.14
            try
            {
                // in OSX, /System/Library/CoreServices/SystemVersion.plist is a XML file a ProductVersion property:
                //      <key>ProductVersion</key>
                //      <string>10.11.1</string>
                // https://www.cyberciti.biz/faq/mac-osx-find-tell-operating-system-version-from-bash-prompt/
                string sysVerFile = @"/System/Library/CoreServices/SystemVersion.plist";
                if (File.Exists(sysVerFile))
                {
                    var content = File.ReadAllText(sysVerFile);
                    var xdoc = XDocument.Parse(content);
                    var dictElement = xdoc.Root.Element("dict");
                    bool productVersionFound = false;
                    if (dictElement != null)
                    {
                        foreach (var k in dictElement.Elements())
                        {
                            if (StringComparer.OrdinalIgnoreCase.Equals(k.Value, "ProductVersion"))
                            {
                                productVersionFound = true;
                            }
                            else if (productVersionFound)
                            {
                                stdOutCallback?.Invoke($"OSX Version: {k.Value}");
                                osxVersion = new Version(k.Value);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                stdErrCallback?.Invoke($"Error {ex.Message} trying to get OSX version. Assume {osxVersion}");
            }

            stdOutCallback?.Invoke($"Found version {osxVersion}");
            return osxVersion;
        }

        private (int exitCode, string userName) DetermineCurrentUser()
        {
            if (this.IsWindows)
            {
                return (0, WindowsIdentity.GetCurrent().Name);
            }

            // For Mac & Linux, run "whoami" to determine current user
            var whoami = this.IsOSX ? "/usr/bin/whoami" : "whoami";
            (var exitCode, var output) = this.ExecuteAndReturnOutput(whoami, arguments: null, timeout: TimeSpan.FromSeconds(1), stdOutCallback: null, stdErrCallback: null);
            return (exitCode, output.Trim());
        }
    }
}