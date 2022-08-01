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
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.Extensions.CommandLineUtils;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe.Commands.Connect
{
    internal class ListIngressCommand : CommandBase, ITopLevelCommand
    {
        private string _targetNamespace;
        private string _targetKubeConfigContext;
        private string _routingHeader;

        public ListIngressCommand(
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

        public override string Name => CommandConstants.ListIngressCommand;

        public override void Configure(CommandLineApplication app)
        {
            this._command = app;
            this._command.ShowInHelpText = false;

            var targetNamespaceOption = _cliCommandOptionFactory.CreateConnectTargetNamespaceOption();
            var targetKubeConfigContextOption = _cliCommandOptionFactory.CreateConnectTargetKubeConfigContextOption();
            var routingOption = _cliCommandOptionFactory.CreateConnectRoutingHeaderOption();

            this._command.Options.Add(targetNamespaceOption);
            this._command.Options.Add(targetKubeConfigContextOption);
            this._command.Options.Add(routingOption);

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
                if (routingOption.HasValue())
                {
                    _routingHeader = routingOption.Value();
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

                KubeConfigDetails kubeConfigDetails;
                using (var kubeConfigClient = _clientFactory.CreateKubeConfigClient(_targetKubeConfigContext))
                {
                    kubeConfigDetails = kubeConfigClient.GetKubeConfigDetails();
                    // If it's not passed, read the namespace from the kubeconfig
                    if (string.IsNullOrWhiteSpace(_targetNamespace))
                    {
                        this._targetNamespace = kubeConfigDetails.NamespaceName;
                    }
                }

                using (var kubernetesManagementClient = _clientFactory.CreateKubernetesManagementClient(kubeConfigDetails))
                {
                    var uris = (await kubernetesManagementClient.ListPublicUrlsInNamespaceAsync(_targetNamespace, this.CancellationToken, _routingHeader)).Value;
                    _out.Data(uris);
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ListIngress, out string failureReason, displayUnkownErrors: true))
            {
                return (ExitCode.Fail, failureReason);
            }

            return (ExitCode.Success, string.Empty);
        }
    }
}