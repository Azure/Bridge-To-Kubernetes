// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { Guid } from 'guid-typescript';
import * as vscode from 'vscode';

import { IBinariesUtility } from './binaries/IBinariesUtility';
import { ConnectWorkspaceFolder } from './connect/ConnectWorkspaceFolder';
import { Constants } from './Constants';
import { DebugAssetsInitializer } from './debug/DebugAssetsInitializer';
import { FileLogWriter } from './logger/FileLogWriter';
import { Logger } from './logger/Logger';
import { TelemetryEvent } from './logger/TelemetryEvent';
import { AccountContextManager } from './models/context/AccountContextManager';
import { StatusBarMenu } from './StatusBarMenu';

// Handles logic related to initialization. For example, provides a unified way to check if the CLI is present.
export class Initializer {
    public constructor(
        private readonly _context: vscode.ExtensionContext,
        private readonly _workspacesCommonId: Guid,
        private readonly _workspaceFolder: vscode.WorkspaceFolder,
        private readonly _logger: Logger,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _outputChannel: vscode.OutputChannel,
        private readonly _binariesUtility: IBinariesUtility
    ) {
    }

    public async initializeConnectWorkspaceFolderAsync(
        fileLogWriter: FileLogWriter,
        statusBarMenu: StatusBarMenu): Promise<ConnectWorkspaceFolder> {
        this._logger.trace(`Connect initialization started on ${this._workspaceFolder.name}`);
        try {
            const connectWorkspaceFolder = new ConnectWorkspaceFolder(
                this._context,
                this._workspaceFolder,
                fileLogWriter,
                this._accountContextManager,
                statusBarMenu,
                this._outputChannel,
                this._binariesUtility);

            this._logger.trace(TelemetryEvent.ConnectInitializationSuccess, {
                workspacesCommonId: this._workspacesCommonId.toString()
            });

            return connectWorkspaceFolder;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.ConnectInitializationError, error, {
                workspacesCommonId: this._workspacesCommonId.toString()
            });
            vscode.window.showErrorMessage(`Failed to initialize '${this._workspaceFolder.name}' for ${Constants.ProductName}: ${error.message}`);
        }
    }

    public async retrieveBridgeConfigurationDebugAssetsPresentInFolderAsync(reason: string): Promise</*debugConfigurationName*/ string> {
        try {
            const wereDebugAssetsEverCreated = this._context.workspaceState.get<boolean>(Constants.ConnectDebugAssetsCreationIdentifier, /*defaultValue*/ false);
            const debugAssetsInitializer = new DebugAssetsInitializer(this._workspaceFolder, this._logger);
            const debugConfigurationName: string = await debugAssetsInitializer.retrieveBridgeConfigurationDebugAssetsAsync(reason, /*shouldCreateDebugAssetsIfMissing*/ !wereDebugAssetsEverCreated);
            this._context.workspaceState.update(Constants.ConnectDebugAssetsCreationIdentifier, wereDebugAssetsEverCreated || debugConfigurationName != null);

            this._logger.trace(TelemetryEvent.Connect_EnsureConfigurationPresentSuccess, {
                checkReason: reason,
                wereDebugAssetsCreated: debugConfigurationName != null
            });

            return debugConfigurationName;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.Connect_EnsureConfigurationPresentError, error, {
                checkReason: reason
            });
            vscode.window.showErrorMessage(`Failed to validate the ${Constants.ProductName} configuration in launch.json for '${this._workspaceFolder.name}'. ${error}`);
        }

        return null;
    }
}