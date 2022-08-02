// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    internal interface ICliCommandOptionFactory
    {
        CliCommandOption CreateParentProcessIdOption();

        CliCommandOption CreateConnectTargetNamespaceOption();

        CliCommandOption CreateConnectTargetContainerOption();

        CliCommandOption CreateConnectTargetPodOption();

        CliCommandOption CreateConnectWithDeploymentOption();

        CliCommandOption CreateConnectTargetServiceOption();

        CliCommandOption CreateConnectTargetKubeConfigContextOption();

        CliCommandOption CreateConnectLocalPortOption();

        CliCommandOption CreateConnectUpdateScriptOption();

        CliCommandOption CreateConnectEnvOption();

        CliCommandOption CreateControlPortOption();

        CliCommandOption CreateConnectElevationRequestsOptions();

        CliCommandOption CreateConnectRoutingHeaderOption();

        CliCommandOption CreateUseKubernetesServiceEnvironmentVariablesOption();

        CliCommandOption CreateRunContainerizedOption();

        CliCommandOption CreateYesOption();

        CliCommandOption CreateRoutingManagerFeatureFlagOption();
    }
}