// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export class Constants {
    /* BinariesUtility V1 & V2 Shared Constants */
    public static readonly BinariesVersionedUrlProd = `https://bridgetokubernetes.blob.core.windows.net/%s/%s/lks.json`;
    public static readonly BinariesVersionedUrlStaging = `https://mindarostaging.blob.core.windows.net/%s/%s/lks.json`;
    public static readonly BinariesVersionedUrlDev = `https://mindaromaster.blob.core.windows.net/%s/%s/lks.json`;

    /* BinariesUtilityV2 Constants */
    public static readonly BinariesLatestVersionUrlProd = `https://aka.ms/bridge-lks-v2`;
    public static readonly BinariesLatestVersionUrlStaging = `https://aka.ms/bridge-lks-staging-v2`;
    public static readonly BinariesLatestVersionUrlDev = `https://aka.ms/bridge-lks-dev-v2`;
    public static readonly LegacyBridgeDownloadDirectoryName = `binaries`;
    public static readonly BridgeDownloadDirectoryName = `bridge`;
    public static readonly KubectlDownloadDirectoryName = `kubectl`;
    public static readonly DotNetDownloadDirectoryName = `dotnet`;
    public static readonly KubectlMinVersion = `1.21.2`;
    public static readonly DotNetMinVersion = `3.1.6`;

    /* BinariesUtility Constants */
    public static readonly CliVersionsUrlProd = `https://aka.ms/bridge-lks`;
    public static readonly CliVersionsUrlStaging = `https://aka.ms/bridge-lks-staging`;
    public static readonly CliVersionsUrlDev = `https://aka.ms/bridge-lks-dev`;
    public static readonly CliDownloadDirectoryName = `binaries`;

    public static readonly ProductName = `Bridge to Kubernetes`;
    public static readonly EndpointManagerName = `EndpointManager`;
    public static readonly ExtensionIdentifier = `mindaro.mindaro`;
    public static readonly ConnectConfigurationDebuggerType = `bridge-to-kubernetes.configuration`;
    public static readonly TaskSource = `bridge-to-kubernetes`;
    public static readonly ConnectResourceTaskType = `${Constants.TaskSource}.resource`;
    public static readonly ConnectCompoundTaskType = `${Constants.TaskSource}.compound`;
    public static readonly ConnectConfigureCommand = `mindaro.configure`;
    public static readonly ConnectDebugAssetsCreationIdentifier = `connectDebugAssetsCreation`;
    public static readonly SettingsConfigurationName = `bridgeToKubernetes`;
    public static readonly SupportedPlatforms: NodeJS.Platform[] = [ `win32`, `darwin`, `linux` ];
    public static readonly AzureDevSpacesMinExtensionVersion = `2.0.220200723`;
    public static readonly FirstCharacterOfMachineIDToUseBinariesUtilityV2 = [ `1`, `2` ];
    public static readonly CodespacesNotSupportedErrorMessage = `Codespaces is currently not supported for ${Constants.ProductName}. Please use local or remote development which is supported on ${Constants.SupportedPlatforms.join(`, `)} platforms.`;
    public static readonly RemoteDevelopmentLearnMoreMessage = `Remote development is only supported using Kubernetes service environment variables. Set 'useKubernetesServiceEnvironmentVariables' task property to 'true' for ${Constants.ProductName}'s task and select the 'Learn More' button to understand the requirements.`;

    // Legacy tasks/configurations/debuggers our users might still use.
    // TODO: Automatically transition these to the new types.
    public static readonly LegacyConnectConfigurationDebuggerType1 = `dev-spaces-connect-configuration`;
    public static readonly LegacyDebuggerType1 = `dev-spaces`;
    public static readonly LegacyTaskSource1 = `dev-spaces`;
    public static readonly LegacyConnectServiceTaskType1 = `${Constants.LegacyTaskSource1}.connect.service`;
    public static readonly LegacyConnectCompoundTaskType1 = `${Constants.LegacyTaskSource1}.connect.compound`;

    public static readonly LegacyConnectConfigurationDebuggerType2 = `local-process-with-kubernetes.configuration`;
    public static readonly LegacyDebuggerType2 = `local-process-with-kubernetes`;
    public static readonly LegacyTaskSource2 = `local-process-with-kubernetes`;
    public static readonly LegacyConnectServiceTaskType2 = `${Constants.LegacyTaskSource2}.service`;
    public static readonly LegacyConnectCompoundTaskType2 = `${Constants.LegacyTaskSource2}.compound`;

    public static readonly LegacyConnectServiceTaskType3 = `${Constants.TaskSource}.service`;

    public static readonly FileDownloaderMinVersion = `1.0.10`;
    public static readonly FileDownloaderVersionError = `${Constants.ProductName} cannot run alongside the extension, File Downloader with version older than '${Constants.FileDownloaderMinVersion}'. Please update the File Downloader extension.`;

    public static readonly ListOfErrorMessagesForUsingKubernetesServiceEnvironmentVariables = [
        `prevent '${Constants.ProductName}' from forwarding network traffic`,
        `The ${Constants.EndpointManagerName} launch process was cancelled`,
        `Failed to launch ${Constants.EndpointManagerName}`,
        `Please free this port and try again. Use 'netstat -ano' command to find which program is using this port`,
        `BranchCache service to free port 80`,
        `The process cannot access the file`,
        `This program is blocked by group policy`
    ];

    // From vscode.Pseudoterminal definition: "Note writing `\n` will just move the cursor down 1 row, you need to write `\r` as well to move the cursor to the left-most cell."
    public static readonly PseudoterminalNewLine = `\r\n`;
}