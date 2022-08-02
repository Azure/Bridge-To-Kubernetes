// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Input;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.Extensions.CommandLineUtils;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe.Commands.Connect
{
    internal class CleanConnectCommand : CommandBase, ITopLevelCommand
    {
        public CleanConnectCommand(
            CommandLineArgumentsManager commandLineArgumentsManager,
            IManagementClientFactory clientFactory,
            ILog log,
            IOperationContext operationContext,
            IConsoleInput consoleInput,
            IConsoleOutput consoleOutput,
            IProgress<ProgressUpdate> progress,
            ICliCommandOptionFactory cliCommandOptionFactory,
            ISdkErrorHandling sdkErrorHandling)
            : base(
                  commandLineArgumentsManager,
                  clientFactory,
                  log,
                  operationContext,
                  consoleInput,
                  consoleOutput,
                  progress,
                  cliCommandOptionFactory,
                  sdkErrorHandling)
        {
        }

        public override string Name => CommandConstants.CleanConnectCommand;

        public override void Configure(CommandLineApplication app)
        {
            this._command = app;
            this._command.ShowInHelpText = false;

            this._command.OnExecute(() =>
            {
                this.SetCommand();
                return 0;
            });
        }

        public async override Task<(ExitCode, string)> ExecuteAsync()
        {
            try
            {
                using (var endpointManagementClient = _clientFactory.CreateEndpointManagementClient())
                {
                    _log.Info($"Ordering {nameof(EndpointManager)} shutdown...");
                    await endpointManagementClient.StopEndpointManagerAsync(CancellationToken);
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.CleanConnect, out string failureReason))
            {
                return (ExitCode.Fail, failureReason);
            }

            return (ExitCode.Success, string.Empty);
        }
    }
}