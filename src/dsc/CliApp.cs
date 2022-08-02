// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading;
using System.Threading.Tasks;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Exe.Commands;
using Microsoft.BridgeToKubernetes.Exe.Telemetry;
using Microsoft.BridgeToKubernetes.Library.Exceptions;
using Microsoft.Extensions.Configuration;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe
{
    /// <summary>
    /// The CLI application
    /// </summary>
    internal class CliApp : AppBase
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 4;

        private readonly ConsoleColor foregroundColor;
        private readonly ConsoleColor backgroundColor;
        private bool resetCursor;

        private readonly CommandLineArgumentsManager _commandLineArgumentsManager;
        private readonly CommandsConfigurator _commandsConfigurator;

        private readonly IEnvironmentVariables _environmentVariables;
        private readonly ILog _log;
        private readonly Lazy<IFileLogger> _fileLogger;
        private readonly Common.IO.Output.IConsoleOutput _out;

        internal IConfigurationRoot Configuration { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="commandLineArgumentsManager">Asking for a Lazy, to prevent circular dependency errors</param>
        public CliApp(
            IConfigurationRoot configuration,
            IEnvironmentVariables environmentVariables,
            ILog log,
            Common.IO.Output.IConsoleOutput consoleOutput,
            Lazy<IFileLogger> fileLogger,
            CommandLineArgumentsManager commandLineArgumentsManager,
            CommandsConfigurator commandConfigurator)
        {
            this.foregroundColor = Console.ForegroundColor;
            this.backgroundColor = Console.BackgroundColor;
            this.Configuration = configuration;
            this._environmentVariables = environmentVariables;
            this._out = consoleOutput;
            this._fileLogger = fileLogger;
            this._commandLineArgumentsManager = commandLineArgumentsManager;
            this._commandsConfigurator = commandConfigurator;
            this._log = log;
        }

        /// <summary>
        /// Main entrypoint
        /// </summary>
        public override int Execute(string[] args, CancellationToken cancellationToken)
        {
            return (int)ExecuteAsync(args, cancellationToken).Result;
        }

        private async Task<ExitCode> ExecuteAsync(string[] args, CancellationToken cancellationToken)
        {
            CommandLogging commandLogging = null;

            try
            {
                commandLogging = new CommandLogging(args, this._log);
            }
            catch (Exception ex)
            {
                this._log.ExceptionAsWarning(ex);
            }

            // This should always be executed before anything else that outputs to console.
            ConfigureConsoleCursor();

            ExitCode commandResult = ExitCode.Success;
            string failureReason = string.Empty;

            using (CancellationTokenSource tokenSource = new CancellationTokenSource())
            using (cancellationToken.Register(() => tokenSource.Cancel()))
            {
                var canceled = false;
                void cancel()
                {
                    if (!canceled)
                    {
                        canceled = true;
                        this._commandLineArgumentsManager.Command.Cleanup(); // Clean up any command-specific processes
                        tokenSource.Cancel();
                    }
                };

                void cancelOnCtrlC(object sender, ConsoleCancelEventArgs eventArgs)
                {
                    _out.Warning(Resources.Warning_Cancelling);
                    cancel();
                    eventArgs.Cancel = true;
                    // In case something deadlocks, forcibly terminate CLI
                    Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ =>
                    {
                        RestoreConsoleState();
                        Environment.Exit((int)ExitCode.ForceTerminate);
                    });
                }
                Console.CancelKeyPress += cancelOnCtrlC;

                void cancelOnAssemblyUnload(AssemblyLoadContext obj)
                {
                    cancel();
                    RestoreConsoleState();
                }
                AssemblyLoadContext.Default.Unloading += cancelOnAssemblyUnload;
                try
                {
                    (commandResult, failureReason) = await RunCommandAsync(args, tokenSource.Token);
                }
                catch (Exception e)
                {
                    failureReason = this.ReportException(e);
                    commandResult = ExitCode.Fail;
                }
                finally
                {
                    Shutdown(commandLogging, commandResult, failureReason);
                    Console.CancelKeyPress -= cancelOnCtrlC;
                    AssemblyLoadContext.Default.Unloading -= cancelOnAssemblyUnload;
                }
            }

            return commandResult;
        }

        private async Task<(ExitCode, string)> RunCommandAsync(string[] args, CancellationToken cancellationToken)
        {
            // Configure Commands
            var configureFailureReason = ConfigureCommands();
            if (!string.IsNullOrWhiteSpace(configureFailureReason))
            {
                return (ExitCode.Fail, configureFailureReason);
            }

            // Check if we have no command after Configure: --help mode
            if (this._commandLineArgumentsManager.Command == null)
            {
                return (ExitCode.Success, string.Empty);
            }

            this._commandLineArgumentsManager.Command.CancellationToken = cancellationToken;

            if (!_environmentVariables.ReleaseEnvironment.IsProduction())
            {
                _out.Verbose($"****** Targeting environment: {_environmentVariables.ReleaseEnvironment} ******");
            }

            // This finally calls the command logic
            _log.Info($"Running {_commandLineArgumentsManager.Command}...");
            var endResult = await this._commandLineArgumentsManager.Command.ExecuteAsync();

            return endResult;
        }

        private void ConfigureConsoleCursor()
        {
            try
            {
                // Setting Console Mode for properly handling ANSI escape codes on windows console
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var handle = GetStdHandle(STD_OUTPUT_HANDLE);
                    GetConsoleMode(handle, out var mode);
                    mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING;
                    SetConsoleMode(handle, mode);
                }

                // Capture the initial cursor visibility
                resetCursor = Console.CursorVisible;

                // Setting Console Cursor Visibility to 'false'
                // not to hinder with any console output
                Console.CursorVisible = false;
            }
            catch (PlatformNotSupportedException)
            {
                // get of CursorVisible is not supported on mac & linux,
                // so just reset the cursor when we are done
                resetCursor = true;
                Console.CursorVisible = false;
            }
            catch (IOException)
            {
                // Cannot get to the cursor so don't reset it later
                resetCursor = false;
            }
        }

        private string ConfigureCommands()
        {
            string failureReason = string.Empty;
            try
            {
                if (this._commandsConfigurator.ConfigureCommands() != 0)
                {
                    failureReason = "Could not configure command";
                }
            }
            catch (OperationIdException ex)
            {
                failureReason = this.ReportException(ex);
                _out.WriteRequestId(ex);
            }
            catch (Exception ex)
            {
                failureReason = this.ReportException(ex);
            }

            return failureReason;
        }

        private string ReportException(Exception exception)
        {
            if (exception is IUserVisibleExceptionReporter
                    || exception is Extensions.CommandLineUtils.CommandParsingException)
            {
                _out.Error(exception.Message);
                _log.ExceptionAsWarning(exception);
                return exception.Message;
            }
            if (exception is AggregateException)
            {
                return this.ReportException(exception.InnerException);
            }
            if (exception is DependencyResolutionException)
            {
                return this.ReportException(exception.InnerException);
            }

            // Unexpected error
            _out.Error(!string.IsNullOrEmpty(exception.Message) ? string.Format(CommonResources.Error_Unexpected, exception.Message) : CommonResources.Error_OopsMessage);
            _out.Error(string.Format(CommonResources.Error_BridgeReport, CliConstants.BridgeReportLink));
            if (!string.IsNullOrWhiteSpace(_fileLogger.Value.CurrentLogDirectoryPath))
            {
                _out.Error(string.Format(CommonResources.Error_SeeLogFile, _fileLogger.Value.CurrentLogDirectoryPath));
            }

            if (exception is IOperationIds operationIds)
            {
                _out.WriteRequestId(operationIds);
                _log.ExceptionAsWarning(exception);
            }
            else
            {
                _log.Exception(exception);
            }

            return exception.Message;
        }

        private void Shutdown(CommandLogging commandLogging, ExitCode result, string failureReason)
        {
            RestoreConsoleState();
            commandLogging?.Finished(result == ExitCode.Success, failureReason);

            // Flush console output
            _out.Flush();

            // Flush logs
            var log = this._commandLineArgumentsManager.Command?.ShouldSendTelemetry ?? true ? this._log : this._log.WithoutTelemetry;
            log.Flush(TimeSpan.FromMilliseconds(1500));
        }

        private void RestoreConsoleState()
        {
            // Reverting Console Cursor Visibility
            if (resetCursor)
            {
                Console.CursorVisible = true;
            }

            // Reverting Console Foreground and Background Color
            Console.ForegroundColor = foregroundColor;
            Console.BackgroundColor = backgroundColor;
        }
    }
}