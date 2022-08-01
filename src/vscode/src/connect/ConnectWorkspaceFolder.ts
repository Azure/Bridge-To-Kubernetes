// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as os from 'os';
import * as portfinder from 'portfinder';
import * as process from 'process';
import * as request from 'request-promise-native';
import * as tmp from 'tmp';
import * as vscode from 'vscode';

import { IExperimentationService } from 'vscode-tas-client';
import { IBinariesUtility } from '../binaries/IBinariesUtility';
import { BridgeClient } from '../clients/BridgeClient';
import { IKubeconfigEnrichedContext, KubectlClient } from '../clients/KubectlClient';
import { Constants } from '../Constants';
import { DebugAssetsInitializer } from '../debug/DebugAssetsInitializer';
import { FileLogWriter } from '../logger/FileLogWriter';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { AccountContextManager } from '../models/context/AccountContextManager';
import { IElevationRequest } from '../models/IElevationRequest';
import { IPromptItem, PromptResult } from '../PromptItem';
import { IStatusBarMenuItem, StatusBarItemGroup, StatusBarMenu } from '../StatusBarMenu';
import { CheckExtensionSupport } from '../utility/CheckExtensionSupport';
import { EventSource, IReadOnlyEventSource, IReleasable } from '../utility/Event';
import { fileSystem } from '../utility/FileSystem';
import { KubeconfigCredentialsManager } from '../utility/KubeconfigCredentialsManager';
import { ThenableUtility } from '../utility/ThenableUtility';
import { UrlUtility } from '../utility/UrlUtility';
import { WorkspaceFolderBase } from '../WorkspaceFolderBase';
import { ConnectWizard } from './ConnectWizard';
import { IWizardOutput } from './IWizardOutput';
import { ResourceType } from './ResourceType';

export interface IConnectionTarget {
    resourceName: string;
    resourceType: ResourceType;
    ports: number[];
    isolateAs: string;
    targetCluster: string;
    targetNamespace: string;
}

// Handles the Connect feature for a specific workspace folder.
export class ConnectWorkspaceFolder extends WorkspaceFolderBase {
    private readonly _connectionOutputEmitted: EventSource<string>;
    private readonly _onRefreshStatusBarMenuItemsTriggeredReleasable: IReleasable;
    private _connectEnvFilePath: string;
    private _connectionStatus = ConnectionStatus.Disconnected;
    private _cmdFilePath: string;
    private _connectionTarget: IConnectionTarget;
    private _controlPort: number;
    private _preConnectStatusBarMenuItems: IStatusBarMenuItem[];
    private _connectStatusBarMenuItems: IStatusBarMenuItem[];
    private _currentConnectCommandPromise: Promise<void>;

    public constructor(
        context: vscode.ExtensionContext,
        workspaceFolder: vscode.WorkspaceFolder,
        fileLogWriter: FileLogWriter,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _statusBarMenu: StatusBarMenu,
        private readonly _outputChannel: vscode.OutputChannel,
        private readonly _binariesUtility: IBinariesUtility) {
        super(context, workspaceFolder, fileLogWriter, /*logIdentifier*/ `Connect`);

        this._preConnectStatusBarMenuItems = [];
        this._connectStatusBarMenuItems = [];
        this._connectionOutputEmitted = new EventSource<string>();

        this._onRefreshStatusBarMenuItemsTriggeredReleasable = this._statusBarMenu.refreshStatusBarMenuItemsTriggered().subscribe(() => {
            this.setConnectStatus(this._connectionStatus, /*forceRefresh*/ true);
        });
    }

    public dispose(): void {
        this._onRefreshStatusBarMenuItemsTriggeredReleasable.release();
    }

    public get connectionStatus(): ConnectionStatus {
        return this._connectionStatus;
    }

    public get connectionTarget(): IConnectionTarget {
        return this._connectionTarget;
    }

    public get connectionOutputEmitted(): IReadOnlyEventSource<string> {
        return this._connectionOutputEmitted;
    }

    public async runConnectWizardAsync(
        wizardReason: string,
        targetResourceName: string = null,
        targetResourceNamespace: string = null,
        targetResourceType: ResourceType = ResourceType.Service
    ): Promise<IWizardOutput> {
        try {
            const connectWizard = new ConnectWizard(this._binariesUtility, this._workspaceFolder, this._logger);
            return await connectWizard.runAsync(wizardReason, targetResourceName, targetResourceNamespace, targetResourceType);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.Connect_Error, error);
            vscode.window.showErrorMessage(`Failed to run the wizard. Error: ${error.message}`);
        }
    }

    public async resolveDebugConfigurationAsync(debugConfiguration: vscode.DebugConfiguration): Promise<vscode.DebugConfiguration> {
        if (debugConfiguration.env == null) {
            debugConfiguration.env = {};
        }

        if (this._connectEnvFilePath != null && await fileSystem.existsAsync(this._connectEnvFilePath)) {
            const content: string = await fileSystem.readFileAsync(this._connectEnvFilePath, `utf8`);
            const env: any = JSON.parse(content);
            this._logger.trace(TelemetryEvent.Connect_DebugConfigApplied);
            for (const key of Object.keys(env)) {
                debugConfiguration.env[key] = env[key];
            }
        }
        return debugConfiguration;
    }

    public async launchTerminalAsync(): Promise<void> {
        if (!await fileSystem.existsAsync(this._cmdFilePath)) {
            vscode.window.showErrorMessage(`Please connect to the cluster first.`);
            return;
        }

        this._logger.trace(TelemetryEvent.Connect_TerminalLaunched);
        const options: vscode.TerminalOptions = {
            name: `Connected terminal (${this._workspaceFolder.name})`,
            cwd: vscode.workspace.workspaceFolders != null && vscode.workspace.workspaceFolders.length > 0 ? vscode.workspace.workspaceFolders[0].uri.fsPath : `.`
        };

        if (process.platform === `win32`) {
            options.shellPath = `cmd.exe`;
            options.shellArgs = [ `/K`, this._cmdFilePath ];
        }
        else {
            if (this._connectEnvFilePath != null && await fileSystem.existsAsync(this._connectEnvFilePath)) {
                const content: string = await fileSystem.readFileAsync(this._connectEnvFilePath, `utf8`);
                options.env = JSON.parse(content);
            }
        }

        const terminal: vscode.Terminal = vscode.window.createTerminal(options);
        terminal.show();
        this._context.subscriptions.push(terminal);
    }

    public async startConnectAsync(
        resourceName: string,
        resourceType: ResourceType,
        ports: number[],
        isolateAs: string,
        targetCluster: string,
        targetNamespace: string,
        useKubernetesServiceEnvironmentVariables: boolean,
        experimentationService: IExperimentationService): Promise</*success*/ boolean> {
        const prerequisitesAlertCallback = CheckExtensionSupport.validatePrerequisites(this._logger, /*validatePostDownloadPrerequisites*/ true, this._workspaceFolder);
        if (prerequisitesAlertCallback != null) {
            prerequisitesAlertCallback();
            return false;
        }

        const bridgeClient: BridgeClient = await this._binariesUtility.tryGetBridgeAsync();
        const kubectlClient: KubectlClient = await this._binariesUtility.tryGetKubectlAsync();
        if (bridgeClient == null || kubectlClient == null) {
            return false;
        }
        this._connectionOutputEmitted.trigger(`Retrieving the current context and credentials...${Constants.PseudoterminalNewLine}`);
        const kubeconfig = await kubectlClient.getCurrentContextAsync();
        if (!await KubeconfigCredentialsManager.refreshCredentialsAsync(kubeconfig.kubeconfigPath, kubeconfig.namespace, bridgeClient, this._logger)) {
            return false;
        }

        if (this.isConnectCommandRunning()) {
            this._logger.warning(`A call to start Connect was made despite the Connect connection status being already 'Connected'. Ignoring the new call.`);
            return true;
        }

        if (this._connectionStatus === ConnectionStatus.Disconnecting && this._currentConnectCommandPromise != null) {
            this._logger.warning(`A call to start Connect was made despite the Connect connection status being 'Disconnecting'. Waiting for the disconnection to complete.`);
            await this._currentConnectCommandPromise;
        }

        // Validates that the current context matches what was declared in the configuration,
        // to help the users detect when the wrong cluster/namespace is set.
        if (targetCluster != null || targetNamespace != null) {
            const currentContext: IKubeconfigEnrichedContext = await kubectlClient.getCurrentContextAsync();
            if ((targetCluster != null && targetCluster !== currentContext.cluster)
                || (targetNamespace != null && targetNamespace !== currentContext.namespace)) {
                const messageParts: string[] = [];
                messageParts.push(`Your current kubeconfig context targets the cluster '${currentContext.cluster}' and namespace '${currentContext.namespace}'.`);
                messageParts.push(`The ${Constants.ProductName} debug configuration was configured for`);
                if (targetCluster != null && targetNamespace != null) {
                    messageParts.push(`cluster '${targetCluster}' and namespace '${targetNamespace}'.`);
                }
                else if (targetCluster != null) {
                    messageParts.push(`cluster '${targetCluster}'.`);
                }
                else {
                    messageParts.push(`namespace '${targetNamespace}'.`);
                }
                messageParts.push(`\nYou can modify the current kubeconfig context targeted using the Kubernetes extension.`);
                messageParts.push(`\n\nAre you sure you want to continue with the current kubeconfig context?`);

                const yesItem: IPromptItem = { title: `Yes, the resource '${resourceName}' exists in the current context`, result: PromptResult.Yes };
                const messageOptions: vscode.MessageOptions = {
                    modal: true
                };
                const selectedItem: IPromptItem = await ThenableUtility.ToPromise(vscode.window.showWarningMessage(messageParts.join(` `), messageOptions, yesItem));
                const promptResult: PromptResult = selectedItem != null ? selectedItem.result : PromptResult.No;
                this._logger.trace(TelemetryEvent.Connect_WrongKubeconfigContextTargetedModalClosed, {
                    result: promptResult.toString()
                });
                switch (promptResult) {
                    case PromptResult.Yes:
                        vscode.window.showWarningMessage(`If the target namespace and cluster configured are incorrect, you can modify them in your tasks.json file.`);
                        break;
                    case PromptResult.No:
                        vscode.window.showInformationMessage(`Change the current cluster, namespace and kubeconfig from the Clusters section of the Kubernetes view.`);
                        await vscode.commands.executeCommand(`workbench.view.extension.kubernetesView`);
                        return false;
                    default:
                        const error = new Error(`Unsupported PromptResult value: ${promptResult}`);
                        this._logger.error(TelemetryEvent.UnexpectedError, error);
                        throw error;
                }
            }
        }

        this._logger.trace(TelemetryEvent.Connect_StartConnect, {
            resource: resourceName,
            resourceType: resourceType,
            ports: ports.toString(),
            isolateAs: isolateAs,
            os: os.platform()
        });

        return new Promise((resolve, reject): void => {
            vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: `Redirecting Kubernetes ${resourceType} "${resourceName}" to your machine...`,
                cancellable: true
            }, async (progress, cancellationToken) => {
                cancellationToken.onCancellationRequested(async () => {
                    this._logger.warning(`Connect command cancelled by the user`);
                    if (this.isConnectCommandRunning()) {
                        await this.stopConnectAsync();
                    }
                    resolve(false);
                });

                this._connectionTarget = {
                    resourceName: resourceName,
                    resourceType: resourceType,
                    ports: ports,
                    isolateAs: isolateAs,
                    targetCluster: targetCluster,
                    targetNamespace: targetNamespace
                };

                this._connectionOutputEmitted.trigger(`Validating the credentials to access the cluster...${Constants.PseudoterminalNewLine}`);
                const kubeconfigPath: string = await this._accountContextManager.getKubeconfigPathAsync();

                if (!await KubeconfigCredentialsManager.refreshCredentialsAsync(kubeconfig.kubeconfigPath, kubeconfig.namespace, bridgeClient, this._logger)) {
                    return resolve(false);
                }

                // If the users want to use Bridge with isolation, we need to validate that their clusters aren't
                // Dev Spaces-enabled, as else the traditional routing system will conflict with the new one.
                if (isolateAs != null) {
                    try {
                        const isNonDevSpacesCluster: boolean = await this.validateNonDevSpacesCluster(kubectlClient, kubeconfigPath);
                        if (!isNonDevSpacesCluster) {
                            return resolve(false);
                        }
                    }
                    catch (error) {
                        return reject(error);
                    }
                }

                this._connectionOutputEmitted.trigger(`Validating the requirements to replicate resources locally...${Constants.PseudoterminalNewLine}`);
                let consentedElevationRequests: IElevationRequest[];
                if (!useKubernetesServiceEnvironmentVariables) {
                    try {
                        consentedElevationRequests = await this.validateElevationRequestsAsync(bridgeClient, kubeconfigPath, resourceType, resourceName, kubeconfig.namespace, cancellationToken);
                        if (consentedElevationRequests == null) {
                            // We need admin permissions to kill some services/process, and/or edit the hosts file but the user didn't give
                            // consent to do so. Stopping the Connect session.
                            return resolve(false);
                        }
                    }
                    catch (error) {
                        return reject(error);
                    }
                }

                this._connectEnvFilePath = `${tmp.tmpNameSync()}.env`;
                this._cmdFilePath = `${this._connectEnvFilePath}.cmd`;

                // Generates a basePort in the range [50000, 60000).
                const basePort = Math.floor(Math.random() * 10000 + 50000);
                this._controlPort = await portfinder.getPortPromise({ port: basePort });

                this.setConnectStatus(ConnectionStatus.Connecting);

                const onOutputEmittedReleasable: IReleasable = bridgeClient.outputEmitted.subscribe((data: string) => {
                    this._outputChannel.append(data);
                    this._connectionOutputEmitted.trigger(data);
                    progress.report({ increment: 10 });
                });

                // Waits for the environment file being created, which indicates the connection completed.
                const environmentFilePromise: Promise<void> = new Promise((resolve): void => {
                    const timer: NodeJS.Timeout = setInterval(async () => {
                        if (await fileSystem.existsAsync(this._connectEnvFilePath)) {
                            clearInterval(timer);
                            resolve();
                        }
                    }, 100);
                });

                this._connectionOutputEmitted.trigger(`Redirecting traffic from the cluster to your machine...${Constants.PseudoterminalNewLine}`);
                const connectCommandPromise: Promise<void> = bridgeClient.connectAsync(
                    /*currentWorkingDirectory*/ this._workspaceFolder.uri.fsPath,
                    kubeconfigPath,
                    resourceName,
                    resourceType,
                    ports,
                    this._controlPort,
                    /*envFilePath*/ this._connectEnvFilePath,
                    /*scriptFilePath*/ this._cmdFilePath,
                    /*parentProcessId*/ process.ppid.toString(),
                    consentedElevationRequests,
                    isolateAs,
                    kubeconfig.namespace,
                    useKubernetesServiceEnvironmentVariables,
                    experimentationService);

                this._currentConnectCommandPromise = connectCommandPromise.then(() => {
                    this._outputChannel.appendLine(`${Constants.ProductName} command terminated successfully.`);
                    progress.report({ increment: 100 });
                    this._connectEnvFilePath = null;
                    this.setConnectStatus(ConnectionStatus.Disconnected);
                }).catch((error) => {
                    const errorMessage = `Failed to establish a connection. Error: ${error.message}`;
                    this._outputChannel.appendLine(errorMessage);
                    this._outputChannel.show();
                    progress.report({ increment: 100 });
                    this.setConnectStatus(ConnectionStatus.Failed);
                    this._logger.error(TelemetryEvent.Connect_Error, error);
                    throw new Error(errorMessage);
                }).finally(() => {
                    onOutputEmittedReleasable.release();
                });

                try {
                    await Promise.race([ environmentFilePromise, this._currentConnectCommandPromise ]);
                }
                catch (error) {
                    // Make sure we reject the current promise, so that the exception is handled properly.
                    return reject(error);
                }

                if (cancellationToken.isCancellationRequested) {
                    return resolve(false);
                }

                progress.report({ increment: 100 });
                await this.delay(200);
                this.setConnectStatus(ConnectionStatus.Connected);
                resolve(true);
            });
        });
    }

    public async stopConnectAsync(): Promise<void> {
        if (!this.isConnectCommandRunning()) {
            vscode.window.showErrorMessage(`No connection to disconnect from.`);
            return;
        }

        const disconnectAfterDebugging: boolean = vscode.workspace.getConfiguration(Constants.SettingsConfigurationName).get<boolean>(`disconnectAfterDebugging`, true);
        if (!disconnectAfterDebugging) {
            // Only show the disconnection status when the setting is to NOT disconnect after debugging,
            // as this is when the user made a manual action and expects a visual feedback.
            vscode.window.showInformationMessage(`Disconnecting from cluster...`);
        }

        const previousStatus: ConnectionStatus = this._connectionStatus;
        this.setConnectStatus(ConnectionStatus.Disconnecting);

        try {
            // Send POST request to http://localhost:<control-port>/api/remoting/stop.
            await request.post({
                method: `POST`,
                uri: `http://localhost:${this._controlPort.toString()}/api/remoting/stop/`,
                body: ``,
                simple: true
            });

            // We triggered an HTTP call to stop the connection, but at this point the connection is still ongoing.
            // Wait for the current Connect command to complete, which means the connection is really disconnected.
            if (this._currentConnectCommandPromise != null) {
                await this._currentConnectCommandPromise;
                if (!disconnectAfterDebugging) {
                    // Only show the disconnection status when the setting is to NOT disconnect after debugging,
                    // as this is when the user made a manual action and expects a visual feedback.
                    vscode.window.showInformationMessage(`Disconnected from cluster.`);
                }
            }

            this.setConnectStatus(ConnectionStatus.Disconnected);
            this._logger.trace(TelemetryEvent.Connect_DisconnectSuccessful);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.Connect_DisconnectError, error);

            if (previousStatus !== ConnectionStatus.Connected && error.code === `ECONNREFUSED`) {
                // We weren't connected to the cluster, so we ignore the error.
                this.setConnectStatus(ConnectionStatus.Disconnected);
                return;
            }

            // Hack while we refactor the cancellation token logic (bug 1160787).
            // In some cases, when disconnecting we get an HTTP 500 error whereas everything went fine.
            if (error.message.startsWith(`500`)) {
                this.setConnectStatus(ConnectionStatus.Disconnected);
                return;
            }

            this.setConnectStatus(ConnectionStatus.Failed);
            vscode.window.showErrorMessage(`Disconnecting from cluster failed: ${error.message}`);
        }
    }

    private setConnectStatus(status: ConnectionStatus, forceRefresh: boolean = false): void {
        if (!forceRefresh && this._connectionStatus === status) {
            return;
        }

        this._connectionStatus = status;
        this.clearPreConnectStatusBarMenuItems();
        this.clearConnectStatusBarMenuItems();
        if (this._connectionStatus === ConnectionStatus.Connected) {
            this.registerConnectedStatusBarMenuItems();
        }
        else if (this._connectionStatus === ConnectionStatus.Disconnected) {
            this.registerDisconnectedStatusBarMenuItemsAsync();
        }
    }

    private registerConnectedStatusBarMenuItems(): void {
        this._connectStatusBarMenuItems.push(this._statusBarMenu.addItem(
            StatusBarItemGroup.Connect, `$(terminal) Open a connected terminal`, /*description*/ this._workspaceFolder.name, async () => {
                await this.launchTerminalAsync();
            }
        ));

        this._connectStatusBarMenuItems.push(this._statusBarMenu.addItem(
            StatusBarItemGroup.Connect, `$(info) Display connection status`, /*description*/ this._workspaceFolder.name, () => {
                if (this._connectionStatus === ConnectionStatus.Connected) {
                    vscode.window.showInformationMessage(`Connected to resource: ${this._connectionTarget.resourceName}`);
                }
                else {
                    vscode.window.showInformationMessage(`Connection status: ${ConnectionStatus[this._connectionStatus]}`);
                }
            }
        ));

        this._connectStatusBarMenuItems.push(this._statusBarMenu.addItem(
            StatusBarItemGroup.Connect, `$(pulse) Show connection diagnostics information`, /*description*/ this._workspaceFolder.name, async () => {
                this._outputChannel.show();
                this._outputChannel.appendLine(`Service connected: ${this._connectionTarget.resourceName}`);

                const serviceIpMap: string = await this.getDnsStatusAsync();
                this._outputChannel.appendLine(``);
                this._outputChannel.appendLine(`Local service IPs:`);
                this._outputChannel.appendLine(serviceIpMap);

                this._outputChannel.appendLine(`Environment variables:`);
                if (this._connectEnvFilePath != null && await fileSystem.existsAsync(this._connectEnvFilePath)) {
                    const content: string = await fileSystem.readFileAsync(this._connectEnvFilePath, `utf8`);
                    const env: any = JSON.parse(content);
                    for (const key of Object.keys(env)) {
                        this._outputChannel.appendLine(`${key}=${env[key]}`);
                    }
                }
            }
        ));

        this._connectStatusBarMenuItems.push(this._statusBarMenu.addItem(
            StatusBarItemGroup.Connect, `$(debug-disconnect) Disconnect current session`, /*description*/ this._workspaceFolder.name, async () => {
                await this.stopConnectAsync();
                this._statusBarMenu.triggerIngressesRefreshAsync();
            }
        ));
    }

    private async registerDisconnectedStatusBarMenuItemsAsync(): Promise<void> {
        const getConnectTaskAsyncFunction = async (): Promise<vscode.Task> => {
            const tasks: vscode.Task[] = await ThenableUtility.ToPromise(vscode.tasks.fetchTasks());
            return tasks.find(task => DebugAssetsInitializer.isConnectTask(task.name) && task.scope === this._workspaceFolder);
        };

        // We only want to display the status bar menu item if we have a valid task.
        const connectTask: vscode.Task = await getConnectTaskAsyncFunction();
        if (connectTask != null) {
            this._preConnectStatusBarMenuItems.push(this._statusBarMenu.addItem(
                StatusBarItemGroup.PreConnect, `$(broadcast) Connect to the cluster`, /*description*/ this._workspaceFolder.name, async () => {
                    try {
                        // We make sure that the task is still valid (might not if the tasks.json changed).
                        const executableConnectTask = await getConnectTaskAsyncFunction();
                        if (executableConnectTask == null) {
                            throw new Error(`Failed to get the task ${connectTask.name} in the tasks.json file.`);
                        }
                        await ThenableUtility.ToPromise(vscode.tasks.executeTask(connectTask));
                        vscode.window.showInformationMessage(`Once connected to the cluster, run your code manually to start debugging.`);
                        this._logger.trace(TelemetryEvent.Connect_StatusBarMenuConnectSuccess);
                    }
                    catch (error) {
                        this._logger.error(TelemetryEvent.Connect_StatusBarMenuConnectError, error);
                        vscode.window.showErrorMessage(`Failed to execute the ${Constants.ProductName} task: ${error.message}`);
                    }
                }
            ));
        }
    }

    private clearPreConnectStatusBarMenuItems(): void {
        for (const statusBarMenuItem of this._preConnectStatusBarMenuItems) {
            this._statusBarMenu.removeItem(StatusBarItemGroup.PreConnect, statusBarMenuItem);
        }
        this._preConnectStatusBarMenuItems = [];
    }

    private clearConnectStatusBarMenuItems(): void {
        for (const statusBarMenuItem of this._connectStatusBarMenuItems) {
            this._statusBarMenu.removeItem(StatusBarItemGroup.Connect, statusBarMenuItem);
        }
        this._connectStatusBarMenuItems = [];
    }

    private async delay(ms: number): Promise<void> {
        return new Promise((resolve): void => {
            // tslint:disable-next-line no-string-based-set-timeout
            setTimeout(resolve, ms);
        });
    }

    private async getDnsStatusAsync(): Promise<string> {
        try {
            // The CLI DNS uses the port 50052.
            return await request.get({
                method: `GET`,
                uri: `http://localhost:50052/api/hosts/info/`
            });
        }
        catch {
            return `Local DNS service is not running.`;
        }
    }

    private async validateElevationRequestsAsync(
        bridgeClient: BridgeClient,
        kubeconfigPath: string,
        resourceType: ResourceType,
        resourceName: string,
        currentNamespace: string,
        cancellationToken: vscode.CancellationToken): Promise<IElevationRequest[]> {
        let isCancelled = false;
        cancellationToken.onCancellationRequested(async () => {
            this._logger.warning(`Connect command cancelled by the user: cancelling elevation requests validation as well`);
            isCancelled = true;
            return Promise.resolve(null);
        });

        const elevationRequests: IElevationRequest[] = await bridgeClient.prepConnectAsync(
            /*currentWorkingDirectory*/ this._workspaceFolder.uri.fsPath,
            kubeconfigPath,
            resourceType,
            resourceName,
            currentNamespace
        );
        if (elevationRequests.length === 0) {
            // We already have everything we need to Connect. No need to ask the user to consent.
            return elevationRequests;
        }

        if (isCancelled) {
            // If we're cancelled, prevent the method from displaying a UI.
            return null;
        }

        let shouldEditHostsFile = false;
        const servicesAndProcessesToDisable: string[] = [];
        const portsToFree: string[] = [];
        for (const elevationRequest of elevationRequests) {
            if (elevationRequest.requestType === `EditHostsFile`) {
                shouldEditHostsFile = true;
            }
            else if (elevationRequest.requestType === `FreePort`) {
                for (const targetPortInformation of elevationRequest.targetPortInformation) {
                    servicesAndProcessesToDisable.push(`${elevationRequest.targetType} ${targetPortInformation.name}`);
                    portsToFree.push(targetPortInformation.port.toString());
                }
            }
        }

        const messageParts: string[] = [];

        messageParts.push(`Debugging with ${Constants.ProductName} uses administrator permissions to:\n`);
        for (let i = 0; i < servicesAndProcessesToDisable.length; i++) {
            messageParts.push(`-   Stop ${servicesAndProcessesToDisable[i]} to free port ${portsToFree[i]}.\n`);
        }

        if (shouldEditHostsFile) {
            messageParts.push(`-   Update your machine's hosts file to match your Kubernetes cluster environment.\n`);
        }

        messageParts.push(`\nOnce your cluster environment is replicated, all processes on your machine will be able to access it.`);

        const yesItem: IPromptItem = { title: `Continue`, result: PromptResult.Yes };
        const messageOptions: vscode.MessageOptions = {
            modal: true
        };
        const selectedItem: IPromptItem = await ThenableUtility.ToPromise(vscode.window.showWarningMessage(messageParts.join(` `), messageOptions, yesItem));
        const promptResult: PromptResult = selectedItem != null ? selectedItem.result : PromptResult.No;
        return promptResult === PromptResult.Yes ? elevationRequests : null;
    }

    private async validateNonDevSpacesCluster(kubectlClient: KubectlClient, kubeconfigPath: string): Promise<boolean> {
        let namespaces: string[];
        try {
            namespaces = await kubectlClient.getNamespacesAsync(kubeconfigPath);
        }
        catch (error) {
            // Note: If for some reason we can't list the namespaces (e.g. the user doesn't have permission), we recover and continue the connect session.
            this._logger.warning(`Failed to list namespaces`, error);
            return true;
        }
        const isDevSpacesCluster: boolean = namespaces.includes(`azds`);
        if (!isDevSpacesCluster) {
            // All good! This cluster is ready to use routing.
            this._logger.trace(TelemetryEvent.Connect_ValidateNonDevSpacesCluster, {
                isDevSpacesCluster: false
            });
            return true;
        }

        const message = `${Constants.ProductName} cannot run isolated on an Azure Dev Spaces enabled cluster. Please disable Azure Dev Spaces on this cluster.`;
        this._outputChannel.append(message);
        this._connectionOutputEmitted.trigger(message);

        const moreInformationItem: IPromptItem = { title: `Disable Azure Dev Spaces`, result: PromptResult.Yes };
        const messageOptions: vscode.MessageOptions = {
            modal: true
        };
        const selectedItem: IPromptItem = await ThenableUtility.ToPromise(vscode.window.showWarningMessage(message, messageOptions, moreInformationItem));
        const selectedToDisableDevSpaces: boolean = selectedItem != null && selectedItem.result === PromptResult.Yes;
        if (selectedToDisableDevSpaces) {
            UrlUtility.openUrl(`https://aka.ms/bridge-to-k8s-disable-dev-spaces`);
        }

        this._logger.trace(TelemetryEvent.Connect_ValidateNonDevSpacesCluster, {
            isDevSpacesCluster: true,
            selectedToDisableDevSpaces: selectedToDisableDevSpaces
        });

        return false;
    }

    private isConnectCommandRunning(): boolean {
        return this._connectionStatus === ConnectionStatus.Connected || this._connectionStatus === ConnectionStatus.Connecting;
    }
}

export enum ConnectionStatus {
    Disconnecting,
    Disconnected,
    Connecting,
    Connected,
    Failed
}