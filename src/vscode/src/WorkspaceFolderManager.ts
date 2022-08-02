// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { Guid } from 'guid-typescript';
import * as vscode from 'vscode';
import { IExperimentationService } from 'vscode-tas-client';

import { IBinariesUtility } from './binaries/IBinariesUtility';
import { ConnectionStatus, ConnectWorkspaceFolder } from './connect/ConnectWorkspaceFolder';
import { IWizardOutput } from './connect/IWizardOutput';
import { ResourceType } from './connect/ResourceType';
import { Constants } from './Constants';
import { DebugAssetsInitializer } from './debug/DebugAssetsInitializer';
import { Initializer } from './Initializer';
import { FileLogWriter } from './logger/FileLogWriter';
import { Logger } from './logger/Logger';
import { TelemetryEvent } from './logger/TelemetryEvent';
import { AccountContextManager } from './models/context/AccountContextManager';
import { IPromptItem, PromptResult } from './PromptItem';
import { StatusBarMenu } from './StatusBarMenu';
import { ConnectServiceTaskTerminal } from './tasks/ConnectServiceTaskTerminal';
import { IReleasable } from './utility/Event';
import { ThenableUtility } from './utility/ThenableUtility';
import { KubernetesPanelCustomizer } from './viewModels/KubernetesPanelCustomizer';

// Handles the workspace folders opened in VS Code. Initializes the workspace
// folders according to the feature enabled on them (Debug, Connect, etc.).
export class WorkspaceFolderManager {
    private readonly OpenSettingsDisabled: string = `openSettingsDisabled`;

    private readonly _connectWorkspaceFolderMap: Map<vscode.WorkspaceFolder, ConnectWorkspaceFolder>;
    private readonly _onCurrentAksClusterChangedReleasable: IReleasable;
    private readonly _connectDebugSessions: Set<vscode.DebugSession>;
    // Used to make sure we're preventing any actions before the Connect workspace folders are loaded and initialized properly.
    private readonly _connectWorkspaceFoldersInitializationPromise: Promise<void>;
    private _latestConnectStartTimestampInMs: number;

    public constructor(
        private readonly _context: vscode.ExtensionContext,
        workspaceFolders: readonly vscode.WorkspaceFolder[],
        private readonly _workspacesCommonId: Guid,
        private readonly _fileLogWriter: FileLogWriter,
        private readonly _logger: Logger,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _kubernetesPanelCustomizer: KubernetesPanelCustomizer,
        private readonly _statusBarMenu: StatusBarMenu,
        private readonly _outputChannel: vscode.OutputChannel,
        private readonly _binariesUtility: IBinariesUtility,
        private readonly _experimentationService: IExperimentationService) {
        this._connectWorkspaceFolderMap = new Map<vscode.WorkspaceFolder, ConnectWorkspaceFolder>();
        this._connectDebugSessions = new Set<vscode.DebugSession>();
        let resolveConnectWorkspaceFoldersInitializationPromise: () => void;
        this._connectWorkspaceFoldersInitializationPromise = new Promise<void>((resolve): void => {
            resolveConnectWorkspaceFoldersInitializationPromise = resolve;
        });

        this._context.subscriptions.push(vscode.commands.registerCommand(Constants.ConnectConfigureCommand, async () => {
            try {
                await this.runConfigureCommandAsync(vscode.workspace.workspaceFolders);
            }
            catch (error) {
                vscode.window.showErrorMessage(`Failed to run the ${Constants.ConnectConfigureCommand} command: ${error.message}`);
                this._logger.error(TelemetryEvent.ConfigureCommandError, error);
            }
        }));

        this._context.subscriptions.push(vscode.workspace.onDidChangeWorkspaceFolders(async workspaceChangeEvent => {
            await this.onDidChangeWorkspaceFoldersAsync(workspaceChangeEvent);
        }));

        this._context.subscriptions.push(vscode.tasks.registerTaskProvider(Constants.ConnectResourceTaskType, {
            provideTasks: (cancellationToken?: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> => {
                return [];
            },
            resolveTask: async (task: vscode.Task, token?: vscode.CancellationToken): Promise<vscode.Task> => {
                return this.resolveConnectTask(task, Constants.ConnectResourceTaskType);
            }
        }));

        this._context.subscriptions.push(vscode.tasks.registerTaskProvider(Constants.LegacyConnectServiceTaskType3, {
            provideTasks: (cancellationToken?: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> => {
                return [];
            },
            resolveTask: async (task: vscode.Task, token?: vscode.CancellationToken): Promise<vscode.Task> => {
                return this.resolveConnectTask(task, Constants.LegacyConnectServiceTaskType3);
            }
        }));

        this._context.subscriptions.push(vscode.tasks.registerTaskProvider(Constants.LegacyConnectServiceTaskType1, {
            provideTasks: (cancellationToken?: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> => {
                return [];
            },
            resolveTask: async (task: vscode.Task, token?: vscode.CancellationToken): Promise<vscode.Task> => {
                return this.resolveConnectTask(task, Constants.LegacyConnectServiceTaskType1);
            }
        }));

        this._context.subscriptions.push(vscode.tasks.registerTaskProvider(Constants.LegacyConnectServiceTaskType2, {
            provideTasks: (cancellationToken?: vscode.CancellationToken): vscode.ProviderResult<vscode.Task[]> => {
                return [];
            },
            resolveTask: async (task: vscode.Task, token?: vscode.CancellationToken): Promise<vscode.Task> => {
                return this.resolveConnectTask(task, Constants.LegacyConnectServiceTaskType2);
            }
        }));

        // Register a DebugConfigurationProvider, so that we intercept debug sessions starting, and can act on them such as
        // adding environment variables to the launched process for Connect.
        this._context.subscriptions.push(vscode.debug.registerDebugConfigurationProvider(`*`, {
            resolveDebugConfigurationWithSubstitutedVariables: async (
                workspaceFolder: vscode.WorkspaceFolder | undefined,
                debugConfiguration: vscode.DebugConfiguration): Promise<vscode.DebugConfiguration> => {
                if (workspaceFolder == null) {
                    // We cannot handle this debugConfiguration. Returning it as is to let other extensions try to deal with it.
                    return debugConfiguration;
                }

                return this.resolveDebugConfigurationAsync(workspaceFolder, debugConfiguration);
            }
        }));

        vscode.debug.onDidStartDebugSession(async (debugSession: vscode.DebugSession) => {
            if (DebugAssetsInitializer.isConnectTask(debugSession.configuration[`preLaunchTask`])) {
                this._logger.trace(TelemetryEvent.Connect_DebugSessionStarted, {
                    launchConfigurationName: debugSession[`name`]
                });

                // Make sure that Connect is actually running. In rare cases, the Connect task might be skipped.
                // In such case, restarting the debug session is enough to be back to a normal state.
                await this._connectWorkspaceFoldersInitializationPromise;
                const workspaceFolder: vscode.WorkspaceFolder = debugSession.workspaceFolder;
                const connectWorkspaceFolder = this._connectWorkspaceFolderMap.get(workspaceFolder);
                if (connectWorkspaceFolder.connectionStatus !== ConnectionStatus.Connected) {
                    // Force the current debug session to stop.
                    vscode.commands.executeCommand(`workbench.action.debug.stop`);
                    this._logger.warning(TelemetryEvent.ConnectPreLaunchTaskSkipped);
                    vscode.window.showErrorMessage(`Failed to redirect the traffic from your service to your local machine. Please restart the debug session.`);
                    return;
                }

                this._connectDebugSessions.add(debugSession);
            }
        });

        vscode.debug.onDidTerminateDebugSession(async (debugSession: vscode.DebugSession) => {
            if (!this._connectDebugSessions.has(debugSession)) {
                return;
            }

            this._connectDebugSessions.delete(debugSession);

            const disconnectAfterDebugging: boolean = vscode.workspace.getConfiguration(Constants.SettingsConfigurationName).get<boolean>(`disconnectAfterDebugging`, true);
            this._logger.trace(TelemetryEvent.Connect_DebugSessionTerminated, {
                launchConfigurationName: debugSession[`name`],
                disconnectAfterDebugging: disconnectAfterDebugging
            });

            const connectWorkspaceFolder: ConnectWorkspaceFolder = this._connectWorkspaceFolderMap.get(debugSession.workspaceFolder);
            if (connectWorkspaceFolder == null) {
                const error = new Error(`Impossible to find the Connect connection corresponding to the Connect debug session ${debugSession.name}`);
                this._logger.error(TelemetryEvent.UnexpectedError, error);
                return;
            }

            if (disconnectAfterDebugging) {
                if (this._latestConnectStartTimestampInMs != null) {
                    const delayInMs = Date.now() - this._latestConnectStartTimestampInMs;
                    if (delayInMs <= 1000) {
                        // The debugging session is terminated and we should disconnect, but a connection was just triggered (less than 1000ms ago).
                        // This is very likely a debugging restart, and so we purposefully don't disconnect in this case. Note that the 1000ms delay
                        // isn't reduced because, when debugging multiple services/depending of the machine, the delay to get the termination can
                        // reach ~500-600ms.
                        this._logger.trace(`Debugging restart (likely) detected: ignoring the session disconnect`);
                        return;
                    }
                }

                await connectWorkspaceFolder.stopConnectAsync();
                this._statusBarMenu.triggerIngressesRefreshAsync();
            }
            this.showOpenSettingMessage(disconnectAfterDebugging);
        });

        if (workspaceFolders != null) {
            this.initializeConnectWorkspaceFoldersAsync(workspaceFolders).then(() => {
                resolveConnectWorkspaceFoldersInitializationPromise();
            });
        }

        this._onCurrentAksClusterChangedReleasable = this._kubernetesPanelCustomizer.currentAksClusterChanged.subscribe(async (aksCluster: string) => {
            if (aksCluster == null) {
                this._logger.warning(`Current cluster has changed, but is not an AKS cluster`);
                return;
            }

            if (vscode.workspace.workspaceFolders == null || vscode.workspace.workspaceFolders.length < 1) {
                this._logger.warning(`No workspace folders found.`);
                return;
            }

            this._logger.warning(`Current cluster has changed to AKS cluster ${aksCluster}`);
            vscode.workspace.workspaceFolders.forEach(async (workspaceFolder: vscode.WorkspaceFolder): Promise<void> => {
                await this.retrieveBridgeConfigurationDebugAssetsPresentInFolderAsync(workspaceFolder, /*reason*/ `CurrentClusterIsAks`);
            });
        });
    }

    public async configureFolderAsync(
        workspaceFolder: vscode.WorkspaceFolder,
        targetResourceName: string = null,
        targetResourceNamespace: string = null,
        targetResourceType: ResourceType = ResourceType.Service
    ): Promise<string> {
        this._logger.trace(TelemetryEvent.ConfigureCommandTriggered);

        const connectDebugConfigurationName: string = await this.runConnectWizardAndConfigurationAsync(
            workspaceFolder,
            /*wizardReason*/ `ConfigurationCommand`,
            targetResourceName,
            targetResourceNamespace,
            targetResourceType
        );
        if (connectDebugConfigurationName != null) {
            vscode.window.showInformationMessage(`The launch configuration '${connectDebugConfigurationName}' was configured successfully.`);
            return connectDebugConfigurationName;
        }
        return null;
    }

    public async pickCommandWorkspaceFolderAsync(workspaceFolders: readonly vscode.WorkspaceFolder[]): Promise<vscode.WorkspaceFolder> {
        if (workspaceFolders == null || workspaceFolders.length < 1) {
            this._logger.warning(`No workspace folders to run the command on`);
            return null;
        }

        let workspaceFolder: vscode.WorkspaceFolder;
        if (workspaceFolders.length === 1) { // Single folder case
            workspaceFolder = workspaceFolders[0];
        }
        else { // Workspace mode
            workspaceFolder = await ThenableUtility.ToPromise(vscode.window.showWorkspaceFolderPick({
                placeHolder: `Pick a workspace folder to run the command`
            }));

            if (workspaceFolder == null) {
                return null;
            }
        }

        return workspaceFolder;
    }

    public async retrieveBridgeConfigurationDebugAssetsPresentInFolderAsync(workspaceFolder: vscode.WorkspaceFolder, checkReason: string): Promise<string> {
        const initializer = new Initializer(
            this._context,
            this._workspacesCommonId,
            workspaceFolder,
            this._logger,
            this._accountContextManager,
            this._outputChannel,
            this._binariesUtility);
        return initializer.retrieveBridgeConfigurationDebugAssetsPresentInFolderAsync(checkReason);
    }

    public dispose(): void {
        this._onCurrentAksClusterChangedReleasable.release();
        this._connectWorkspaceFolderMap.forEach((connectWorkspaceFolder: ConnectWorkspaceFolder) => connectWorkspaceFolder.dispose());
    }

    private async resolveConnectTask(task: vscode.Task, connectTaskType: string): Promise<vscode.Task> {
        if (task.source !== `Workspace`) {
            this._logger.warning(`The Connect task ${task.name} was triggered for an unsupported task source: ${task.source}`);
            return undefined;
        }

        const workspaceFolder = task.scope as vscode.WorkspaceFolder;
        if (workspaceFolder == null) {
            this._logger.warning(`The Connect task ${task.name} was triggered for an unsupported task scope: ${task.scope}`);
            return undefined;
        }

        await this._connectWorkspaceFoldersInitializationPromise;
        const connectWorkspaceFolder: ConnectWorkspaceFolder = this._connectWorkspaceFolderMap.get(workspaceFolder);
        if (connectWorkspaceFolder == null) {
            this._logger.error(TelemetryEvent.UnexpectedError, new Error(`Failed to retrieve the ConnectWorkspaceFolder corresponding to the task ${task.name} in folder ${workspaceFolder.name}`));
            return undefined;
        }

        let resourceName: string = task.definition[`resource`];
        let resourceType: ResourceType = null;
        switch (task.definition[`resourceType`] != null ? task.definition[`resourceType`].toLowerCase() : null) {
            case `service`:
                resourceType = ResourceType.Service;
                break;
            case `pod`:
                resourceType = ResourceType.Pod;
                break;
            default:
                if (task.definition[`service`] != null) {
                    resourceType = ResourceType.Service;
                    resourceName = task.definition[`service`];
                    break;
                }
                vscode.window.showErrorMessage(`Resource type '${task.definition[`resourceType`]}' not supported. Supported types are: ${Object.keys(ResourceType).join(`, `)}`);
                this._logger.error(TelemetryEvent.UnsupportedTargetResourceType, new Error(`Resource type '${task.definition[`resourceType`]}' not supported`));
                return undefined;
        }

        const ports: number[] = task.definition[`ports`];
        const isolateAs: string = task.definition[`isolateAs`];
        const targetCluster: string = task.definition[`targetCluster`];
        const targetNamespace: string = task.definition[`targetNamespace`];
        const useKubernetesServiceEnvironmentVariablesValue: any = task.definition[`useKubernetesServiceEnvironmentVariables`];
        const useKubernetesServiceEnvironmentVariables: boolean = useKubernetesServiceEnvironmentVariablesValue != null ? useKubernetesServiceEnvironmentVariablesValue : false;

        return new vscode.Task(
            task.definition,
            task.scope,
            task.name,
            task.source,
            new vscode.CustomExecution(async (): Promise<vscode.Pseudoterminal> =>
                new ConnectServiceTaskTerminal(
                    this._context,
                    connectWorkspaceFolder,
                    resourceName,
                    resourceType,
                    ports,
                    isolateAs,
                    targetCluster,
                    targetNamespace,
                    useKubernetesServiceEnvironmentVariables,
                    this._experimentationService,
                    this._binariesUtility,
                    this._logger,
                    /*connectStartedCallback*/ (alreadyConnected: boolean): void => {
                        // If routing is enabled, we will refresh the ingress list to only show routing ingresses.
                        // If routing is not enabled (isolateAs == null), we will ensure that we don't have such ingresses.
                        this._statusBarMenu.triggerIngressesRefreshAsync(isolateAs);

                        if (alreadyConnected) {
                            // If we're already connected, stores when the latest connection was made as we use this information
                            // to detect debugging restart and not disconnect, in such case, on debugging session ending.
                            this._latestConnectStartTimestampInMs = Date.now();
                        }
                    }
                )
            ),
            task.problemMatchers);
    }

    private async showOpenSettingMessage(disconnectAfterDebugging: boolean): Promise<void> {
        const isOpenSettingDisabled: boolean = this._context.globalState.get<boolean>(this.OpenSettingsDisabled, /*defaultValue*/ false);
        if (!isOpenSettingDisabled) {
            const informationMessage: string = disconnectAfterDebugging ?
                `Speed up your debugging by keeping the connection to your service alive.`
                : `Your service is still redirected to your local machine. Use the "Kubernetes" status bar menu to disconnect.`;
            const openSettingItem: IPromptItem = { title: `Open Settings`, result: PromptResult.Yes };
            const neverShowItem: IPromptItem = { title: `Never Show Again`, result: PromptResult.No };
            const selectedItem: IPromptItem = await ThenableUtility.ToPromise(vscode.window.showInformationMessage(informationMessage, openSettingItem, neverShowItem));

            if (selectedItem == null) {
                return;
            }

            if (selectedItem.result === PromptResult.Yes) {
                vscode.commands.executeCommand(`workbench.action.openSettings`, `Disconnect After Debugging`);
            }
            else {
                this._context.globalState.update(this.OpenSettingsDisabled, true);
            }
        }
    }

    private async runConfigureCommandAsync(workspaceFolders: readonly vscode.WorkspaceFolder[]): Promise<void> {
        this._logger.trace(TelemetryEvent.ConfigureCommandTriggered);

        if (workspaceFolders == null || workspaceFolders.length < 1) {
            this._logger.warning(`No workspace folders available to configure ${Constants.ProductName}`);
            vscode.window.showWarningMessage(`A folder must be opened in Visual Studio Code to configure ${Constants.ProductName}. Please open the folder you want to configure and try again.`);
            return;
        }

        const workspaceFolder: vscode.WorkspaceFolder = await this.pickCommandWorkspaceFolderAsync(workspaceFolders);
        if (workspaceFolder == null) {
            this._logger.warning(`No workspace folder was selected to configure ${Constants.ProductName}`);
            return;
        }

        await this.runConnectWizardAndConfigurationAsync(workspaceFolder, /*wizardReason*/ `ConfigurationCommand`);
    }

    private async onDidChangeWorkspaceFoldersAsync(workspaceChangeEvent: vscode.WorkspaceFoldersChangeEvent): Promise<void> {
        const addedFolders = workspaceChangeEvent.added;
        const removedFolders = workspaceChangeEvent.removed;

        this._logger.trace(`Workspace folders update`, {
            workspaceFoldersAdded: addedFolders.map((workspaceFolder: vscode.WorkspaceFolder) => workspaceFolder.name).join(`, `),
            workspaceFoldersRemoved: removedFolders.map((workspaceFolder: vscode.WorkspaceFolder) => workspaceFolder.name).join(`, `)
        });

        if (addedFolders != null && addedFolders.length > 0) {
            await this.initializeConnectWorkspaceFoldersAsync(addedFolders);
        }

        if (removedFolders != null && removedFolders.length > 0) {
            for (const workspaceFolder of removedFolders) {
                if (this._connectWorkspaceFolderMap.has(workspaceFolder)) {
                    this._connectWorkspaceFolderMap.delete(workspaceFolder);
                }
            }
        }
    }

    private async initializeConnectWorkspaceFoldersAsync(workspaceFolders: readonly vscode.WorkspaceFolder[]): Promise<void[]> {
        if (workspaceFolders == null || workspaceFolders.length < 1) {
            return;
        }

        const initializationPromises: Promise<void>[] = [];
        workspaceFolders.forEach(workspaceFolder => {
            const initializer: Initializer = new Initializer(
                this._context,
                this._workspacesCommonId,
                workspaceFolder,
                this._logger,
                this._accountContextManager,
                this._outputChannel,
                this._binariesUtility);

            if (!this._connectWorkspaceFolderMap.has(workspaceFolder)) {
                initializationPromises.push(this.tryInitializeConnectWorkspaceFolderAsync(workspaceFolder, initializer));
            }
        });

        return Promise.all(initializationPromises);
    }

    private async tryInitializeConnectWorkspaceFolderAsync(workspaceFolder: vscode.WorkspaceFolder, initializer: Initializer): Promise<void> {
        this._logger.trace(`Trying to initialize the workspace folder ${workspaceFolder.name} for Connect`);

        const connectWorkspaceFolder: ConnectWorkspaceFolder = await initializer.initializeConnectWorkspaceFolderAsync(
            this._fileLogWriter,
            this._statusBarMenu);

        if (connectWorkspaceFolder == null) {
            const error = new Error(`Failed to create the Connect workspace folder for folder ${workspaceFolder.name}`);
            this._logger.error(TelemetryEvent.UnexpectedError, error);
            throw error;
        }

        this._connectWorkspaceFolderMap.set(workspaceFolder, connectWorkspaceFolder);
    }

    private async resolveDebugConfigurationAsync(
        workspaceFolder: vscode.WorkspaceFolder,
        debugConfiguration: vscode.DebugConfiguration
    ): Promise<vscode.DebugConfiguration> {
        this._logger.trace(`Retrieving debug configuration for workspace folder ${workspaceFolder.name}`);

        if (debugConfiguration == null) {
            this._logger.warning(`Debug configuration provided is not defined`);
            return debugConfiguration;
        }

        if (DebugAssetsInitializer.isConnectConfiguration(debugConfiguration.type)) {
            const connectDebugConfigurationName: string = await this.runConnectWizardAndConfigurationAsync(workspaceFolder, /*wizardReason*/ `ConfigurationLaunchProfile`);
            if (connectDebugConfigurationName != null) {
                // Start the Connect debug configuration we just created.
                await vscode.debug.startDebugging(workspaceFolder, connectDebugConfigurationName);
            }
            return undefined;
        }

        const connectWorkspaceFolder = this._connectWorkspaceFolderMap.get(workspaceFolder);
        return connectWorkspaceFolder != null ? connectWorkspaceFolder.resolveDebugConfigurationAsync(debugConfiguration) : debugConfiguration;
    }

    private async runConnectWizardAndConfigurationAsync(
        workspaceFolder: vscode.WorkspaceFolder,
        wizardReason: string,
        targetResourceName: string = null,
        targetResourceNamespace: string = null,
        targetResourceType: ResourceType = ResourceType.Service
    ): Promise<string> {
        await this._connectWorkspaceFoldersInitializationPromise;
        const connectWorkspaceFolder: ConnectWorkspaceFolder = this._connectWorkspaceFolderMap.get(workspaceFolder);
        if (connectWorkspaceFolder == null) {
            const error = new Error(`Failed to retrieve the Connect workspace folder for folder ${workspaceFolder.name}`);
            this._logger.error(TelemetryEvent.UnexpectedError, error);
            throw error;
        }
        const wizardOutput: IWizardOutput = await connectWorkspaceFolder.runConnectWizardAsync(
            wizardReason,
            targetResourceName,
            targetResourceNamespace,
            targetResourceType
        );
        if (wizardOutput == null) {
            return null;
        }

        const debugAssetsInitializer = new DebugAssetsInitializer(workspaceFolder, this._logger);
        const connectDebugConfigurationName: string = await debugAssetsInitializer.configureConnectResourceDebugAssetsAsync(
            wizardOutput.resourceName,
            wizardOutput.resourceType,
            wizardOutput.ports,
            wizardOutput.launchConfigurationName,
            wizardOutput.isolateAs,
            wizardOutput.targetCluster,
            wizardOutput.targetNamespace
        );

        if (wizardOutput.launchConfigurationName == null) {
            vscode.window.showInformationMessage(`The ${Constants.ProductName} task was configured successfully. Use the "Kubernetes" status bar menu to connect to your cluster.`);
        }
        else if (connectDebugConfigurationName != null) {
            vscode.window.showInformationMessage(`The launch configuration '${connectDebugConfigurationName}' was configured successfully.`);
        }

        return connectDebugConfigurationName;
    }
}