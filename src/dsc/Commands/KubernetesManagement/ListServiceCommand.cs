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
    internal class ListServiceCommand : CommandBase, ITopLevelCommand
    {
        private string _targetNamespace;
        private string _targetKubeConfigContext;

        public ListServiceCommand(
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

        public override string Name => CommandConstants.ListServiceCommand;

        public override void Configure(CommandLineApplication app)
        {
            this._command = app;
            this._command.ShowInHelpText = false;

            var targetNamespaceOption = _cliCommandOptionFactory.CreateConnectTargetNamespaceOption();
            var targetKubeConfigContextOption = _cliCommandOptionFactory.CreateConnectTargetKubeConfigContextOption();

            this._command.Options.Add(targetNamespaceOption);
            this._command.Options.Add(targetKubeConfigContextOption);

            this._command.OnExecute(() =>
            {
                if (targetNamespaceOption.HasValue())
                {
                    _targetNamespace = targetNamespaceOption.Value();
                }
                if (targetKubeConfigContextOption.HasValue())
                {
                    _targetKubeConfigContext = targetKubeConfigContextOption.Value();
                }
                this.SetCommand();
                return 0;
            });
        }

        public async override Task<(ExitCode, string)> ExecuteAsync()
        {
            try
            {
                this.OnExecute();
                using (var kubeConfigClient = _clientFactory.CreateKubeConfigClient(_targetKubeConfigContext))
                {
                    var kubeConfigDetails = kubeConfigClient.GetKubeConfigDetails();

                    // If it's not passed, read the namespace from the kubeconfig
                    if (string.IsNullOrWhiteSpace(_targetNamespace))
                    {
                        this._targetNamespace = kubeConfigDetails.NamespaceName;
                    }
                    using (var kubernetesManagementClient = _clientFactory.CreateKubernetesManagementClient(kubeConfigDetails))
                    {
                        var services = (await kubernetesManagementClient.ListServicesInNamespacesAsync(_targetNamespace, this.CancellationToken, excludeSystemServices: true)).Value;
                        _out.Data(services);
                    }
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ListService, out string failureReason, displayUnkownErrors: true))
            {
                return (ExitCode.Fail, failureReason);
            }

            return (ExitCode.Success, string.Empty);
        }
    }
}