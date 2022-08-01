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
    internal class RefreshCredentialsCommand : CommandBase, ITopLevelCommand
    {
        private string _targetKubeConfigContext;
        private string _targetNamespace;

        public RefreshCredentialsCommand(
            CommandLineArgumentsManager commandLineArgumentsManager,
            IManagementClientFactory managementClientFactory,
            ILog log,
            IOperationContext operationContext,
            IConsoleInput consoleInput,
            IConsoleOutput consoleOutput,
            IProgress<ProgressUpdate> progress,
            ICliCommandOptionFactory cliCommandOptionFactory,
            ISdkErrorHandling sdkErrorHandling)
            : base(
                  commandLineArgumentsManager,
                  managementClientFactory,
                  log,
                  operationContext,
                  consoleInput,
                  consoleOutput,
                  progress,
                  cliCommandOptionFactory,
                  sdkErrorHandling)
        { }

        public override string Name => CommandConstants.RefreshCredentialsCommand;

        public override void Configure(CommandLineApplication app)
        {
            this._command = app;
            this._command.ShowInHelpText = false;

            var targetNamespaceOption = _cliCommandOptionFactory.CreateConnectTargetNamespaceOption();
            var targetKubeConfigContextOption = _cliCommandOptionFactory.CreateConnectTargetKubeConfigContextOption();

            this._command.Options.Add(targetKubeConfigContextOption);
            this._command.Options.Add(targetNamespaceOption);

            this._command.OnExecute(() =>
            {
                if (targetNamespaceOption.HasValue())
                {
                    _targetNamespace = targetNamespaceOption.Value();
                }
                if (targetKubeConfigContextOption.HasValue())
                {
                    _operationContext.LoggingProperties.Add(CliConstants.Properties.TargetKubeConfigContextName, new PII(targetKubeConfigContextOption.Value()));
                    _targetKubeConfigContext = targetKubeConfigContextOption.Value();
                }
                this.SetCommand();
                return 0;
            });
        }

        public override async Task<(ExitCode, string)> ExecuteAsync()
        {
            try
            {
                this.OnExecute();
                using (var kubeConfigManagementClient = _clientFactory.CreateKubeConfigClient(_targetKubeConfigContext))
                using (var kubernetesManagementClient = _clientFactory.CreateKubernetesManagementClient(kubeConfigManagementClient.GetKubeConfigDetails()))
                {
                    if (string.IsNullOrEmpty(_targetNamespace))
                    {
                        var kubeConfigDetails = kubeConfigManagementClient.GetKubeConfigDetails();
                        this._targetNamespace = !string.IsNullOrWhiteSpace(kubeConfigDetails.NamespaceName) ? kubeConfigDetails.NamespaceName : throw new InvalidOperationException("Missing required flag: '--namespace'");
                    }
                    await kubernetesManagementClient.RefreshCredentialsAsync(_targetNamespace, this.CancellationToken);
                    return (ExitCode.Success, "");
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ListContext, out string failureReason, displayUnkownErrors: true))
            {
                return (ExitCode.Fail, failureReason);
            }
        }
    }
}