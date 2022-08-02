// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Input;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Exe.Output.Models;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.Extensions.CommandLineUtils;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe.Commands.Connect
{
    internal class PrepConnectCommand : TargetConnectCommandBase, ITopLevelCommand
    {
        public PrepConnectCommand(
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

        public override string Name => CommandConstants.PrepConnect;

        public override void Configure(CommandLineApplication app)
        {
            this._command = app;
            this._command.ShowInHelpText = false;
            base.Configure(app);

            this._command.OnExecute(() =>
            {
                try
                {
                    this.ParseTargetOptions();
                }
                catch (Exception ex)
                {
                    _out.Error(ex.Message);
                    this._command.ShowHelp();
                    return 1;
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
                    // If it's not passed, read the namespace from the kubeconfig
                    if (string.IsNullOrWhiteSpace(_targetNamespace))
                    {
                        this._targetNamespace = kubeConfigClient.GetKubeConfigDetails().NamespaceName;
                    }

                    RemoteContainerConnectionDetails remoteContainerConnectionDetails = this.ResolveContainerConnectionDetails(routingHeaderValue: null, routingManagerFeatureFlags: null);
                    // TODO (lolodi): Everything local (e.g. useKubernetesEnvVar or runContainerized can be grouped in a localConnectionDetails class)
                    using (var connectManagementClient = _clientFactory.CreateConnectManagementClient(remoteContainerConnectionDetails, kubeConfigClient.GetKubeConfigDetails(), useKubernetesServiceEnvironmentVariables: false, runContainerized: false))
                    {
                        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
                        var elevationRequests = await connectManagementClient.GetElevationRequestsAsync(cancellationTokenSource.Token);
                        var elevationRequestsData = elevationRequests.Select(er => new ElevationRequestData(er));
                        _out.Data(elevationRequestsData);
                    }
                }
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.PrepConnect, out string failureReason))
            {
                return (ExitCode.Fail, failureReason);
            }

            return (ExitCode.Success, string.Empty);
        }
    }
}