// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { Guid } from 'guid-typescript';
import { Telemetry } from 'telaug';
import * as vscode from 'vscode';
import TelemetryReporter from 'vscode-extension-telemetry';

import { BinariesManager } from './binaries/BinariesManager';
import { IBinariesUtility } from './binaries/IBinariesUtility';
import { Constants } from './Constants';
import { LocalTunnelDebuggingManager } from './debug/LocalTunnelDebuggingManager';
import { createExperimentationServiceAsync, ExperimentationTelemetry } from './ExperimentationService';
import { FileLogWriter } from './logger/FileLogWriter';
import { Logger } from './logger/Logger';
import { TelemetryEvent } from './logger/TelemetryEvent';
import { AccountContextManager } from './models/context/AccountContextManager';
import { StatusBarMenu } from './StatusBarMenu';
import { fileSystem } from './utility/FileSystem';
import { VersionUtility } from './utility/VersionUtility';
import { KubernetesPanelCustomizer } from './viewModels/KubernetesPanelCustomizer';
import { WorkspaceFolderManager } from './WorkspaceFolderManager';

export class ExtensionRoot {
    private _workspaceFolderManager: WorkspaceFolderManager;
    private _reporter: TelemetryReporter;
    private _fileLogWriter: FileLogWriter;
    private _logger: Logger;
    private _kubernetesPanelCustomizer: KubernetesPanelCustomizer;

    public async activateAsync(context: vscode.ExtensionContext): Promise<void> {
        // Check whether any pre-reqs to be satisfied before the extension can be initialized
        if (this.shouldStopInitialization()) {
            const extensionInitializationErrorMessage = (): void => {
                vscode.window.showErrorMessage(`${Constants.ProductName} cannot run alongside Azure Dev Spaces version older than '${Constants.AzureDevSpacesMinExtensionVersion}'. Please uninstall or update to the latest version of the Azure Dev Spaces extension.`);
            };
            context.subscriptions.push(vscode.commands.registerCommand(Constants.ConnectConfigureCommand, () => {
                extensionInitializationErrorMessage();
            }));

            context.subscriptions.push(vscode.commands.registerCommand(StatusBarMenu.OpenMenuCommand, () => {
                extensionInitializationErrorMessage();
            }));

            extensionInitializationErrorMessage();
            return;
        }

        // Initialize the status bar menu first so that a spinner is shown while the extension is being
        // initialized.
        const statusBarMenu = new StatusBarMenu(context);

        // Initialize loggers
        this._fileLogWriter = new FileLogWriter(context);
        await this._fileLogWriter.initializeAsync();
        this._logger = new Logger(this._fileLogWriter, `Common Extension Root`);

        // Initialize telemetry.
        const packageJsonPath: string = context.asAbsolutePath(`./package.json`);
        let packageJsonContent: object;
        try {
            const packageJsonRawContent: string = await fileSystem.readFileAsync(packageJsonPath, `utf8`);
            packageJsonContent = JSON.parse(packageJsonRawContent);
            if (packageJsonContent == null) {
                throw new Error(`Parsing the package.json file at path ${packageJsonPath} returned null`);
            }
        }
        catch (error) {
            const userFriendlyError = new Error(`Failed to retrieve the package.json file at path ${packageJsonPath}. Error: ${error.message}`);
            this._logger.error(TelemetryEvent.UnexpectedError, userFriendlyError);
            throw userFriendlyError;
        }

        const extensionVersion: string = packageJsonContent[`version`];
        const userAgent = `VSCode/${extensionVersion}`;
        const commandEnvironmentVariables = { BRIDGE_SOURCE_USER_AGENT: userAgent };
        // Copy the main process environment variables in the enviroment variables to use when running commands.
        Object.assign(commandEnvironmentVariables, process.env);

        const accountContextManager = new AccountContextManager(this._logger);

        const workspacesCommonId = Guid.create();

        this._reporter = new TelemetryReporter(
            packageJsonContent[`name`],
            extensionVersion,
            packageJsonContent[`aiKey`],
            /*firstParty*/ true);
        Telemetry.init(this._reporter, /*featureName*/ null, () => this._fileLogWriter.getLastLogsAsync(), /*errorToString*/ null);

        // Initialize ExP
        const experimentationService = await createExperimentationServiceAsync(context, new ExperimentationTelemetry(this._logger));

        const expectedCLIVersion: string = await VersionUtility.getExpectedCliVersionAsync(context, experimentationService, packageJsonContent, this._logger);
        const binariesUtility: IBinariesUtility = BinariesManager.getBinariesUtility(this._logger, context, commandEnvironmentVariables, accountContextManager, expectedCLIVersion);

        this._kubernetesPanelCustomizer = new KubernetesPanelCustomizer(binariesUtility, this._logger);

        const outputChannel: vscode.OutputChannel = vscode.window.createOutputChannel(Constants.ProductName);
        outputChannel.appendLine(`${Constants.ProductName} initialization (VS Code v${vscode.version} - Extension v${extensionVersion})`);
        outputChannel.appendLine(`Logs: ${this._logger.logFilePath}`);
        context.subscriptions.push(outputChannel);

        this._workspaceFolderManager = new WorkspaceFolderManager(
            context,
            vscode.workspace.workspaceFolders,
            workspacesCommonId,
            this._fileLogWriter,
            this._logger,
            accountContextManager,
            this._kubernetesPanelCustomizer,
            statusBarMenu,
            outputChannel,
            binariesUtility,
            experimentationService);

        // Load hook into the Kubernetes extension & register ourselves as a Local Tunnel Debug Provider
        const localTunnelDebuggingManager = new LocalTunnelDebuggingManager(this._workspaceFolderManager, this._logger);
        if (!await localTunnelDebuggingManager.registerLocalTunnelDebugProviderAsync()) {

            // We log this error rather than throwing, because we want to give the rest of the extension a chance to succeed at activation.
            this._logger.error(TelemetryEvent.UnexpectedError, new Error(`Unable to complete activation: failed to register the Local Tunnel debug provider.`));
        }

        await this._kubernetesPanelCustomizer.initializeAsync();

        await statusBarMenu.initializeAsync(this._logger, binariesUtility, accountContextManager);

        this._logger.trace(TelemetryEvent.Activation, {
            workspacesCommonId: workspacesCommonId.toString(),
            workspaceFoldersCount: (vscode.workspace.workspaceFolders != null ? vscode.workspace.workspaceFolders.length : 0).toString()
        });
        this._logger.trace(`Extension activated successfully`);
    }

    public async deactivateAsync(): Promise<void> {
        this._logger.trace(`Extension deactivation`);
        this._workspaceFolderManager.dispose();
        await Telemetry.endAllPendingEvents();
        await this._reporter.dispose();
        await this._logger.closeAsync();
    }

    // Includes all pre-reqs checks to determine if the extension should be activated or not.
    private shouldStopInitialization(): boolean {
        const legacyExtension: vscode.Extension<any> = vscode.extensions.getExtension(`azuredevspaces.azds`);
        if (legacyExtension == null) {
            return false;
        }

        const legacyExtensionVersion: string = legacyExtension.packageJSON[`version`];
        try {
            return !VersionUtility.isVersionSufficient(legacyExtensionVersion, Constants.AzureDevSpacesMinExtensionVersion);
        }
        catch {
            return false; // Don't stop extension activation if we failed to determine the min version requirement
        }
    }
}