// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Input;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.Extensions.CommandLineUtils;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    internal abstract class CommandBase : ICommand
    {
        protected IProgress<ProgressUpdate> _progress;
        protected CommandLineApplication _command;
        protected readonly CommandLineArgumentsManager _commandLineArgumentsManager;
        protected readonly IManagementClientFactory _clientFactory;
        protected readonly ILog _log;
        protected readonly IOperationContext _operationContext;
        protected readonly IConsoleOutput _out;
        protected readonly IConsoleInput _consoleInput;
        protected readonly ICliCommandOptionFactory _cliCommandOptionFactory;
        protected readonly ISdkErrorHandling _sdkErrorHandling;

        public abstract string Name { get; }

        public virtual bool ShouldSendTelemetry { get; set; } = true;

        /// <summary>
        /// Main CancellationToken, this gets cancelled when the application gets killed, e.g. Ctrl+C
        /// </summary>
        public CancellationToken CancellationToken { get; set; }

        public abstract void Configure(CommandLineApplication app);

        public abstract Task<(ExitCode, string)> ExecuteAsync();

        public CommandBase(
            CommandLineArgumentsManager commandLineArgumentsManager,
            IManagementClientFactory clientFactory,
            ILog log,
            IOperationContext operationContext,
            IConsoleInput consoleInput,
            IConsoleOutput consoleOutput,
            IProgress<ProgressUpdate> progress,
            ICliCommandOptionFactory cliCommandOptionFactory,
            ISdkErrorHandling sdkErrorHandling)
        {
            this._commandLineArgumentsManager = commandLineArgumentsManager;
            this._clientFactory = clientFactory;
            this._log = log;
            this._operationContext = operationContext;
            this._out = consoleOutput;
            this._consoleInput = consoleInput;
            this._cliCommandOptionFactory = cliCommandOptionFactory;
            this._sdkErrorHandling = sdkErrorHandling;
            this._progress = progress;
        }

        public void ConfigureHelp(string description, string extendedHelpText = "")
        {
            this._command.Description = description;
            this._command.FullName = description;
            this._command.ExtendedHelpText = extendedHelpText;
            this._command.HelpOption($"{CommandConstants.Options.Help.Short}|{CommandConstants.Options.Help.Long}");
        }

        protected virtual void OnExecute()
        {
        }

        /// <summary>
        /// Commands call this on their OnExecute() so they can persist themselves as the selected command in the CommandLineArgumentsManager
        /// </summary>
        protected void SetCommand()
        {
            this._commandLineArgumentsManager.Command = this;
        }

        protected bool ConfirmContinue(Confirmation defaultConfirmation, string confirmationMessage)
        {
            var confirmationQuestion = defaultConfirmation == Confirmation.Yes ? "(Y/n)" : "(y/N)";
            while (true)
            {
                _out.Info($"{confirmationMessage} {confirmationQuestion}: ", false);
                string userResponse = _consoleInput.ReadLine();
                _out.Info(string.Empty, true);
                if (string.IsNullOrEmpty(userResponse))
                {
                    return defaultConfirmation == Confirmation.Yes;
                }

                if (StringComparer.OrdinalIgnoreCase.Equals(userResponse, "Y"))
                {
                    return true;
                }

                if (StringComparer.OrdinalIgnoreCase.Equals(userResponse, "N"))
                {
                    return false;
                }
            }
        }

        protected enum Confirmation
        {
            Yes,
            No
        }

        public virtual void Cleanup()
        {
        }
    }
}