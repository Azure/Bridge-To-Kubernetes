// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
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
    internal class ListContextCommand : CommandBase, ITopLevelCommand
    {
        public ListContextCommand(
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

        public override string Name => CommandConstants.ListContextCommand;

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

        public override Task<(ExitCode, string)> ExecuteAsync()
        {
            try
            {
                this.OnExecute();

                KubeConfigDetails kubeConfigDetails = null;
                using (var kubeConfigClient = _clientFactory.CreateKubeConfigClient())
                {
                    kubeConfigDetails = kubeConfigClient.GetKubeConfigDetails();
                }

                var clusterToServerMappings = new Dictionary<string, string>();
                var contextResults = new List<KubeConfigContext>();

                try
                {
                    var k8SConfiguration = kubeConfigDetails.Configuration;
                    if (k8SConfiguration == null)
                    {
                        throw new NullReferenceException(nameof(k8SConfiguration));
                    }

                    k8SConfiguration.Clusters.ExecuteForEach((cluster) =>
                    {
                        clusterToServerMappings[cluster.Name] = cluster.ClusterEndpoint.Server;
                    });

                    k8SConfiguration.Contexts.ExecuteForEach((context) =>
                    {
                        contextResults.Add(new KubeConfigContext(
                            current: StringComparer.OrdinalIgnoreCase.Equals(k8SConfiguration.CurrentContext, context.Name),
                            name: context.Name,
                            cluster: context.ContextDetails.Cluster,
                            server: clusterToServerMappings.ContainsKey(context.ContextDetails.Cluster) ? clusterToServerMappings[context.ContextDetails.Cluster] : string.Empty,
                            user: context.ContextDetails.User,
                            namespaceName: context.ContextDetails.Namespace));
                    });
                }
                catch (Exception e)
                {
                    _log.Warning("Failed to load kubeconfig: {0}", new PII(e.Message));
                    throw new InvalidKubeConfigFileException(CommonResources.FailedToLoadKubeConfigFormat, Troubleshooting.FailedToLoadKubeConfigLink);
                }

                _out.Data(contextResults);
            }
            catch (FileNotFoundException e)
            {
                // If we can't get the kubeconfig file from disk,ignore the error and let the code return an empty list of contexts.
                _log.Warning("Failed to load kubeconfig file: '{0}'", new PII(e.Message));
                _out.Data(new List<KubeConfigContext>());
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ListContext, out string failureReason, displayUnkownErrors: true))
            {
                return Task.FromResult((ExitCode.Fail, failureReason));
            }
            return Task.FromResult((ExitCode.Success, string.Empty));
        }
    }
}