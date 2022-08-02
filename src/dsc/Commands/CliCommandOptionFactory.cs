// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    internal class CliCommandOptionFactory : ICliCommandOptionFactory
    {
        public CliCommandOption CreateParentProcessIdOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ParentProcessId.Option,
                CommandConstants.Options.ParentProcessId.Description,
                CommandOptionType.SingleValue,
                isRequired: false);
        }

        public CliCommandOption CreateConnectTargetNamespaceOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectTargetNamespace.Option,
                CommandConstants.Options.ConnectTargetNamespace.Description,
                CommandOptionType.SingleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateConnectTargetContainerOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectTargetContainer.Option,
                CommandConstants.Options.ConnectTargetContainer.Description,
                CommandOptionType.SingleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateConnectTargetPodOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectTargetPod.Option,
                CommandConstants.Options.ConnectTargetPod.Description,
                CommandOptionType.SingleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateConnectWithDeploymentOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectWithDeployment.Option,
                CommandConstants.Options.ConnectWithDeployment.Description,
                CommandOptionType.SingleValue,
                isRequired: false);
        }

        public CliCommandOption CreateConnectTargetServiceOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectTargetService.Option,
                CommandConstants.Options.ConnectTargetService.Description,
                CommandOptionType.SingleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateConnectTargetKubeConfigContextOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.TargetKubeConfigContext.Option,
                CommandConstants.Options.TargetKubeConfigContext.Description,
                CommandOptionType.SingleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateConnectLocalPortOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectLocalPort.Option,
                CommandConstants.Options.ConnectLocalPort.Description,
                CommandOptionType.MultipleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateConnectUpdateScriptOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectUpdateScript.Option,
                CommandConstants.Options.ConnectUpdateScript.Description,
                CommandOptionType.SingleValue,
                isRequired: false);
        }

        public CliCommandOption CreateConnectEnvOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ConnectEnv.Option,
                CommandConstants.Options.ConnectEnv.Description,
                CommandOptionType.SingleValue,
                isRequired: false);
        }

        public CliCommandOption CreateControlPortOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.ControlPort.Option,
                CommandConstants.Options.ControlPort.Description,
                CommandOptionType.SingleValue,
                isRequired: false);
        }

        public CliCommandOption CreateConnectElevationRequestsOptions()
        {
            return new CliCommandOption(
                CommandConstants.Options.ElevationRequests.Option,
                CommandConstants.Options.ElevationRequests.Description,
                CommandOptionType.SingleValue,
                isRequired: false);
        }

        public CliCommandOption CreateConnectRoutingHeaderOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.Routing.Option,
                CommandConstants.Options.Routing.Description,
                CommandOptionType.SingleValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateUseKubernetesServiceEnvironmentVariablesOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.UseKubernetesServiceEnvironmentVariables.Option,
                CommandConstants.Options.UseKubernetesServiceEnvironmentVariables.Description,
                CommandOptionType.NoValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateYesOption()
        {
            return new CliCommandOption(
                shortOption: CommandConstants.Options.Yes.Short,
                longOption: CommandConstants.Options.Yes.Long,
                description: CommandConstants.Options.Yes.Description,
                type: CommandOptionType.NoValue,
                isRequired: false,
                showInHelpText: true);
        }

        public CliCommandOption CreateRoutingManagerFeatureFlagOption()
        {
            return new CliCommandOption(
                shortOption: CommandConstants.Options.RoutingManagerFeatureFlag.Short,
                longOption: CommandConstants.Options.RoutingManagerFeatureFlag.Long,
                description: CommandConstants.Options.RoutingManagerFeatureFlag.Description,
                type: CommandOptionType.MultipleValue,
                isRequired: false,
                showInHelpText: false);
        }

        public CliCommandOption CreateRunContainerizedOption()
        {
            return new CliCommandOption(
                CommandConstants.Options.RunContainerized.Option,
                CommandConstants.Options.RunContainerized.Description,
                CommandOptionType.NoValue,
                isRequired: false,
                showInHelpText: false);
        }
    }
}