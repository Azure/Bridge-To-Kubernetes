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
    internal class RoutingHeaderCommand : CommandBase, ITopLevelCommand
    {
        private string _targetKubeConfigContext;

        public RoutingHeaderCommand(
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
        { }

        public override string Name => CommandConstants.RoutingHeaderCommand;

        public override void Configure(CommandLineApplication app)
        {
            var targetKubeConfigContextOption = _cliCommandOptionFactory.CreateConnectTargetKubeConfigContextOption();
            this._command = app;
            this._command.ShowInHelpText = false;
            this._command.Options.Add(targetKubeConfigContextOption);
            this._command.OnExecute(() =>
            {
                if (targetKubeConfigContextOption.HasValue())
                {
                    _targetKubeConfigContext = targetKubeConfigContextOption.Value();
                }
                this.SetCommand();
                return 0;
            });
        }

        public override Task<(ExitCode, string)> ExecuteAsync()
        {
            try
            {
                this.OnExecute();
                using (var kubeConfigClient = _clientFactory.CreateKubeConfigClient(_targetKubeConfigContext))
                {
                    using (var kubernetesManagementClient = _clientFactory.CreateKubernetesManagementClient(kubeConfigClient.GetKubeConfigDetails()))
                    {
                        var header = kubernetesManagementClient.GetRoutingHeader(this.CancellationToken).Value;
                        _out.Data(new[] { header });
                    }
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.RoutingHeader, out string failureReason, displayUnkownErrors: true))
            {
                return Task.FromResult((ExitCode.Fail, failureReason));
            }

            return Task.FromResult((ExitCode.Success, string.Empty));
        }
    }
}