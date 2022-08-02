// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    internal class KubectlImpl : IKubectlImpl
    {
        private const int MaxBufferSize = 1000;

        protected readonly ILog _log;
        protected readonly IOperationContext _operationContext;
        protected readonly IFileSystem _fileSystem;
        private readonly IPlatform _platform;
        protected readonly IEnvironmentVariables _environmentVariables;
        private readonly string _kubectlFilePath;

        private static class ExecutableLocation
        {
            public static string ParentDirectory = "kubectl";

            public static class Windows
            {
                public static string Name = "kubectl.exe";
                public static string Directory = "win";
            }

            public static class OSX
            {
                public static string Name = "kubectl";
                public static string Directory = "osx";
            }

            public static class Linux
            {
                public static string Name = "kubectl";
                public static string Directory = "linux";
            }
        }

        public KubectlImpl(
            IFileSystem fileSystem,
            IPlatform platform,
            IEnvironmentVariables environmentVariables,
            ILog log,
            string kubectlFilePath = null)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            this._fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this._platform = platform ?? throw new ArgumentNullException(nameof(platform));
            this._environmentVariables = environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
            this._kubectlFilePath = kubectlFilePath;
        }

        /// <summary>
        /// <see cref="IKubectlImpl.RunShortRunningCommand"/>
        /// </summary>
        public int RunShortRunningCommand(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut,
            Action<string> onStdErr,
            CancellationToken cancellationToken,
            Dictionary<string, string> envVariables = null,
            bool log137ExitCodeErrorAsWarning = false,
            int timeoutMs = 30000)
        {
            _log.Info("Invoking kubectl {0} command: {1}", commandName.ToString(), new PII(command));
            Stopwatch w = new Stopwatch();
            var stdOutBuilder = new FixedSizeStringBuilder(MaxBufferSize);
            var stdErrBuilder = new FixedSizeStringBuilder(MaxBufferSize);

            var kubectlProcess = _platform.CreateProcess(
                new ProcessStartInfo()
                {
                    FileName = GetKubectlFilepath(),
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

            if (envVariables != null)
            {
                foreach (KeyValuePair<string, string> env in envVariables)
                {
                    kubectlProcess.StartInfo.EnvironmentVariables[env.Key] = env.Value;
                }
            }
            try
            {
                Action killKubectlProcess =
                    () =>
                    {
                        try
                        {
                            if (!kubectlProcess.HasExited)
                            {
                                kubectlProcess.Kill();
                                kubectlProcess.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Exception(e);
                        }
                    };

                // Creates a waiter that ensures all process output has drained before returning.
                using (cancellationToken.Register(killKubectlProcess))
                using (var outputWaitHandle = new AutoResetEvent(false))
                {
                    DataReceivedEventHandler outputDataReceivedHandler = (sender, e) =>
                    {
                        // The last call will have e.Data as null, so we wait for that condition
                        // to be true to close the handle.
                        if (e.Data == null)
                        {
                            try
                            {
                                outputWaitHandle.Set();
                            }
                            catch (ObjectDisposedException)
                            {
                                // this could be invoked outside of this 'using' statement during timeout
                            }
                        }
                        else
                        {
                            stdOutBuilder.AppendLine(e.Data);
                            onStdOut?.Invoke(e.Data);
                        }
                    };

                    DataReceivedEventHandler errorDataReceivedHandler = (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            stdErrBuilder.AppendLine(e.Data);
                            onStdErr?.Invoke(e.Data);
                        }
                    };

                    try
                    {
                        kubectlProcess.OutputDataReceived += outputDataReceivedHandler;
                        kubectlProcess.ErrorDataReceived += errorDataReceivedHandler;

                        w.Start();
                        kubectlProcess.Start();
                        kubectlProcess.BeginOutputReadLine();
                        kubectlProcess.BeginErrorReadLine();
                        if (kubectlProcess.WaitForExit(timeoutMs) && outputWaitHandle.WaitOne(timeoutMs))
                        {
                            w.Stop();
                            _log.Info("Invoked kubectl {0} command: '{1}' exited with {2} in {3}ms", commandName.ToString(), new PII(command), kubectlProcess.ExitCode, w.ElapsedMilliseconds);
                            if (kubectlProcess.ExitCode != 0)
                            {
                                LogFailure(commandName, command, kubectlProcess.ExitCode, stdOutBuilder, stdErrBuilder, log137ExitCodeErrorAsWarning);
                            }
                            return kubectlProcess.ExitCode;
                        }
                        else
                        {
                            var messageFormat = "'kubectl' timeout after {0}ms when running {1}";
                            _log.Info(messageFormat, timeoutMs, commandName.ToString());
                            throw new TimeoutException(string.Format(messageFormat, timeoutMs, command));
                        }
                    }
                    finally
                    {
                        kubectlProcess.OutputDataReceived -= outputDataReceivedHandler;
                        kubectlProcess.ErrorDataReceived -= errorDataReceivedHandler;
                    }
                }
            }
            catch (Exception e)
            {
                if (e is KubectlException)
                {
                    _log.ExceptionAsWarning(e);
                }
                else
                {
                    _log.Exception(e);
                }

                _log.Error("kubectl {0} command: '{1}' failed with exception {2}", commandName.ToString(), new PII(command), e.Message);
                // Killing and disposing failed process
                if (!kubectlProcess.HasExited)
                {
                    // Try and catch exceptions in case of race conditions or other unexpected errors killing the process
                    try
                    {
                        kubectlProcess.Kill();
                    }
                    catch (Exception kubeEx)
                    {
                        _log.Warning("Failed to kill kubectl {0}", kubeEx.Message);
                    }
                }
                throw;
            }
            finally
            {
                kubectlProcess.Dispose();
            }
        }

        /// <summary>
        /// <see cref="IKubectlImpl.RunLongRunningCommand"/>
        /// </summary>
        public int RunLongRunningCommand(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut,
            Action<string> onStdErr,
            CancellationToken cancellationToken,
            Dictionary<string, string> envVariables = null,
            bool log137ExitCodeErrorAsWarning = false)
        {
            Debug.Assert(cancellationToken != default(CancellationToken), "CancellationToken cannot be passed as default for long running operations");
            _log.Info("Invoking kubectl {0} command: {1}", commandName.ToString(), new PII(command));
            Stopwatch w = new Stopwatch();
            var stdOutBuilder = new FixedSizeStringBuilder(MaxBufferSize);
            var stdErrBuilder = new FixedSizeStringBuilder(MaxBufferSize);

            var kubectlProcess = _platform.CreateProcess(
                new ProcessStartInfo()
                {
                    FileName = GetKubectlFilepath(),
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });

            if (envVariables != null)
            {
                foreach (KeyValuePair<string, string> env in envVariables)
                {
                    kubectlProcess.StartInfo.EnvironmentVariables[env.Key] = env.Value;
                }
            }
            try
            {
                Action killKubectlProcess =
                    () =>
                    {
                        try
                        {
                            if (!kubectlProcess.HasExited)
                            {
                                kubectlProcess.Kill();
                                kubectlProcess.Dispose();
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Exception(e);
                        }
                    };

                kubectlProcess.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        stdOutBuilder.AppendLine(e.Data);
                        onStdOut?.Invoke(e.Data);
                    }
                };

                kubectlProcess.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        stdErrBuilder.AppendLine(e.Data);
                        onStdErr?.Invoke(e.Data);
                    }
                };
                w.Start();
                kubectlProcess.Start();
                using (cancellationToken.Register(killKubectlProcess))
                {
                    kubectlProcess.BeginOutputReadLine();
                    kubectlProcess.BeginErrorReadLine();

                    _log.Info("Invoked long running kubectl {0} command: '{1}'", commandName.ToString(), new PII(command));
                    if (stdErrBuilder.Length > 0)
                    {
                        LogFailure(commandName, command, -1, stdOutBuilder, stdErrBuilder, log137ExitCodeErrorAsWarning);
                    }

                    kubectlProcess.WaitForExit();
                }

                return 0;
            }
            catch (Exception e)
            {
                if (e is KubectlException)
                {
                    _log.ExceptionAsWarning(e);
                }
                else
                {
                    _log.Exception(e);
                }

                _log.Error("kubectl {0} command: '{1}' failed with exception {2}", commandName.ToString(), new PII(command), e.Message);
                // Killing and disposing failed process
                if (!kubectlProcess.HasExited)
                {
                    // Try and catch exceptions in case of race conditions or other unexpected errors killing the process
                    try
                    {
                        kubectlProcess.Kill();
                    }
                    catch (Exception kubeEx)
                    {
                        _log.Warning("Failed to kill kubectl {0}", kubeEx.Message);
                    }
                }
                throw;
            }
            finally
            {
                kubectlProcess.Dispose();
            }
        }

        private void LogFailure(KubernetesCommandName commandName, string command, int exitCode, FixedSizeStringBuilder stdOutBuilder, FixedSizeStringBuilder stdErrBuilder, bool logErrorAsWarning = false)
        {
            string errorMessageFormat = "kubectl {0} command: '{1}' terminated with exit code '{2}'";
            var errorMessageParameters = new List<object>
            {
                commandName.ToString(),
                new PII(command),
                exitCode
            };

            LogIfMaxBufferLengthReached(stdOutBuilder, nameof(stdOutBuilder), commandName);
            LogIfMaxBufferLengthReached(stdErrBuilder, nameof(stdErrBuilder), commandName);

            var stdOut = stdOutBuilder.ToString();
            var stdErr = stdErrBuilder.ToString();

            if (!string.IsNullOrWhiteSpace(stdOut))
            {
                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    errorMessageFormat += " and stdout '{3}' and stderr '{4}'.";
                    errorMessageParameters.Add(new PII(stdOut));
                    // We don't log stdErr as PII because we need it for diagnostic purposes
                    errorMessageParameters.Add(stdErr);
                }
                else
                {
                    errorMessageFormat += " and stdout '{3}'.";
                    errorMessageParameters.Add(new PII(stdOut));
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(stdErr))
                {
                    errorMessageFormat += " and stderr '{3}'.";
                    // We don't log stdErr as PII because we need it for diagnostic purposes
                    errorMessageParameters.Add(stdErr);
                }
                else
                {
                    errorMessageFormat += ".";
                }
            }

            if (logErrorAsWarning)
            {
                _log.Warning(errorMessageFormat, errorMessageParameters.ToArray());
            }
            else
            {
                _log.Error(errorMessageFormat, errorMessageParameters.ToArray());
            }
        }

        public void LogIfMaxBufferLengthReached(FixedSizeStringBuilder builder, string builderName, KubernetesCommandName commandName)
        {
            if (builder.MaxLengthReached)
            {
                _log.Info($"Max buffer size reached in '{builderName}' ({builder.MaxLength} characters) while running a '{commandName.ToString()}' command");
            }
        }

        /// <summary>
        /// Builds a path to the kubectl executable, assuming kubectl was downloaded as part of the build pipeline / as part of dsc.csproj for local build.
        /// The relative path kubectl is installed to: "./kubectl/[win|osx|linux]"
        /// </summary>
        /// <returns>Absolute path to a kubectl executable for the current OS</returns>
        /// <exception cref="KubectlException">If the kubectl path can't be determined because the current OS isn't recognized</exception>
        private string GetKubectlFilepath()
        {
            if (!string.IsNullOrEmpty(_kubectlFilePath))
            {
                return _kubectlFilePath;
            }

            string directoryName;
            string executableName;

            if (this._platform.IsWindows)
            {
                directoryName = ExecutableLocation.Windows.Directory;
                executableName = ExecutableLocation.Windows.Name;
            }
            else if (this._platform.IsOSX)
            {
                directoryName = ExecutableLocation.OSX.Directory;
                executableName = ExecutableLocation.OSX.Name;
            }
            else if (this._platform.IsLinux)
            {
                directoryName = ExecutableLocation.Linux.Directory;
                executableName = ExecutableLocation.Linux.Name;
            }
            else
            {
                _log.Error("Failed to determine runtime OS for kubectl.");
                throw new KubectlException(CommonResources.KubectlNotSupportedMessage);
            }

            var kubectlPath = _fileSystem.Path.Combine(_fileSystem.Path.GetExecutingAssemblyDirectoryPath(), ExecutableLocation.ParentDirectory, directoryName, executableName);
            AssertHelper.True(_fileSystem.FileExists(kubectlPath), $"Private copy of kubectl not found at expected location: '{kubectlPath}'");
            _log.Verbose($"Using kubectl found at: '{kubectlPath}'");

            return kubectlPath;
        }
    }
}