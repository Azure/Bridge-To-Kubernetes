// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
    internal class ListNamespaceCommand : CommandBase, ITopLevelCommand
    {
        private string _targetKubeConfigContext;

        public ListNamespaceCommand(
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

        public override string Name => CommandConstants.ListNamespaceCommand;

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

        public async override Task<(ExitCode, string)> ExecuteAsync()
        {
            try
            {
                this.OnExecute();
                using (var kubeConfigClient = _clientFactory.CreateKubeConfigClient(_targetKubeConfigContext))
                {
                    using (var kubernetesManagementClient = _clientFactory.CreateKubernetesManagementClient(kubeConfigClient.GetKubeConfigDetails()))
                    {
                        IEnumerable<string> spaces = null;
                        try
                        {
                            spaces = (await kubernetesManagementClient.ListNamespacesAsync(this.CancellationToken, excludeReservedNamespaces: true)).Value;
                        }
                        catch (Exception e)
                        {
                            _log.ExceptionAsWarning(e);
                        }

                        if (spaces == null)
                        {
                            // If we fail to get the namespaces, resolve the namespace from the kubeconfig to support scenarios where due to RBAC rules, listing namespaces are not supported (OpenShift, etc.)
                            var kubeConfigDetails = kubeConfigClient.GetKubeConfigDetails();
                            spaces = !string.IsNullOrWhiteSpace(kubeConfigDetails.NamespaceName) ? new List<string> { kubeConfigDetails.NamespaceName } : new List<string>();
                        }
                        _out.Data(spaces);
                    }
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ListNamespace, out string failureReason, displayUnkownErrors: true))
            {
                return (ExitCode.Fail, failureReason);
            }

            return (ExitCode.Success, string.Empty);
        }
    }
}