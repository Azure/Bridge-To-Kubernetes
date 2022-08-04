// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as path from 'path';
import * as vscode from 'vscode';
import { ResourceType } from '../connect/ResourceType';

import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { fileSystem } from '../utility/FileSystem';
import { ObjectUtility } from '../utility/ObjectUtility';
import { ThenableUtility } from '../utility/ThenableUtility';

export class DebugAssetsInitializer {
    private static readonly LegacyTraditionalDevSpacesIdentifierSuffix: string = ` (AZDS)`;
    private static readonly TraditionalDevSpacesIdentifierSuffix: string = ` (Dev Spaces)`;

    public static isTraditionalDevSpacesDebugConfiguration(configurationName: string): boolean {
        return configurationName != null
            && (configurationName.endsWith(this.TraditionalDevSpacesIdentifierSuffix)
                || configurationName.endsWith(this.LegacyTraditionalDevSpacesIdentifierSuffix));
    }

    public static isConnectConfiguration(configurationType: string): boolean {
        return configurationType === Constants.ConnectConfigurationDebuggerType
            || configurationType === Constants.LegacyConnectConfigurationDebuggerType1
            || configurationType === Constants.LegacyConnectConfigurationDebuggerType2;
    }

    public static isConnectTask(taskLabel: string): boolean {
        return taskLabel != null
            && (taskLabel.startsWith(Constants.TaskSource)
                || taskLabel.startsWith(Constants.LegacyTaskSource1)
                || taskLabel.startsWith(Constants.LegacyTaskSource2));
    }

    public constructor(
        private readonly _workspaceFolder: vscode.WorkspaceFolder,
        private readonly _logger: Logger) {
    }

    public async retrieveBridgeConfigurationDebugAssetsAsync(reason: string, shouldCreateDebugAssetsIfMissing: boolean): Promise</*debugConfigurationName*/ string> {
        const launchConfig: vscode.WorkspaceConfiguration = vscode.workspace.getConfiguration(`launch`, this._workspaceFolder.uri);
        let debugConfigurations: object[] = launchConfig.get<{}[]>(`configurations`, /*defaultValue*/ []);

        for (let i = debugConfigurations.length - 1; i >= 0; i--) {
            const configurationType: string = debugConfigurations[i][`type`];
            if (DebugAssetsInitializer.isConnectConfiguration(configurationType)) {
                this._logger.trace(TelemetryEvent.Connect_EnsureConfigurationPresentCancelled, {
                    checkReason: reason,
                    cancellationReason: `ConnectConfigurationAlreadyExists`
                });
                return debugConfigurations[i][`name`];
            }

            const preLaunchTask: string = debugConfigurations[i][`preLaunchTask`];
            if (preLaunchTask != null && DebugAssetsInitializer.isConnectTask(preLaunchTask)) {
                this._logger.trace(TelemetryEvent.Connect_EnsureConfigurationPresentCancelled, {
                    checkReason: reason,
                    cancellationReason: `ConnectLaunchConfigurationAlreadyConfigured`
                });
                return debugConfigurations[i][`name`];
            }
        }

        // Validates that there is at least one launch profile registered. Else, Connect wouldn't make sense as we rely on one.
        if (debugConfigurations.length === 0) {
            this._logger.trace(TelemetryEvent.Connect_EnsureConfigurationPresentCancelled, {
                checkReason: reason,
                cancellationReason: `NoLocalLaunchConfigurations`
            });
            return null;
        }

        if (!shouldCreateDebugAssetsIfMissing) {
            // We don't want to recreate a debug confiuration if the user manually removed one previously
            this._logger.trace(TelemetryEvent.Connect_EnsureConfigurationPresentCancelled, {
                checkReason: reason,
                cancellationReason: `ConnectConfigurationAlreadyAddedInThePast`
            });
            return null;
        }

        let debugConfigurationName: string = null;
        try {
            const templateFilePath: string = path.join(__dirname, `template`, `connect-configuration`, `launch.json`);
            const templateFileContent: string = await fileSystem.readFileAsync(templateFilePath, `utf8`);
            const connectConfiguration: object[] = JSON.parse(templateFileContent);
            debugConfigurationName = connectConfiguration[`name`];
            debugConfigurations = debugConfigurations.concat(connectConfiguration);
            await ThenableUtility.ToPromise(launchConfig.update(`configurations`, debugConfigurations, vscode.ConfigurationTarget.WorkspaceFolder));
        }
        catch (error) {
            this._logger.error(TelemetryEvent.UnexpectedError, new Error(`Something went wrong while loading the template files: ${error.message}`));
            throw error;
        }

        this._logger.trace(`Connect configuration is added to launch.json at ${this._workspaceFolder.uri.fsPath}`);
        return debugConfigurationName;
    }

    public async configureConnectResourceDebugAssetsAsync(
        resourceName: string,
        resourceType: ResourceType,
        ports: number[],
        launchConfigurationName: string,
        isolateAs: string,
        targetCluster: string,
        targetNamespace: string): Promise</*connectDebugConfigurationName*/ string> {
        const launchConfig: vscode.WorkspaceConfiguration = vscode.workspace.getConfiguration(`launch`, this._workspaceFolder.uri);
        let debugConfigurations: object[] = launchConfig.get<{}[]>(`configurations`, /*defaultValue*/ []);
        const tasksConfig: vscode.WorkspaceConfiguration = vscode.workspace.getConfiguration(`tasks`, this._workspaceFolder.uri);
        let tasks: object[] = tasksConfig.get<{}[]>(`tasks`, /*defaultValue*/ []);

        const sourceDebugConfiguration: object = debugConfigurations.find(debugConfiguration => debugConfiguration[`name`] === launchConfigurationName);
        if (launchConfigurationName != null && sourceDebugConfiguration == null) {
            throw new Error(`Failed to retrieve the source launch configuration of name '${launchConfigurationName}' in launch.json`);
        }

        // Clean all existing Connect debug and tasks configurations.
        debugConfigurations = debugConfigurations.filter((debugConfiguration) => {
            return !DebugAssetsInitializer.isConnectConfiguration(debugConfiguration[`type`])
                && !DebugAssetsInitializer.isConnectTask(debugConfiguration[`preLaunchTask`]);
        });
        tasks = tasks.filter((task) => {
            return !DebugAssetsInitializer.isConnectTask(task[`label`]);
        });

        // Create the Connect task.
        let connectPreLaunchTaskName: string = Constants.ConnectResourceTaskType;
        const connectPreLaunchTask = {
            label: Constants.ConnectResourceTaskType,
            type: Constants.ConnectResourceTaskType,
            resource: resourceName,
            resourceType: resourceType,
            ports: ports,
            targetCluster: targetCluster,
            targetNamespace: targetNamespace,
            useKubernetesServiceEnvironmentVariables: false
        };

        if (isolateAs != null) {
            connectPreLaunchTask[`isolateAs`] = isolateAs;
        }

        tasks = tasks.concat(connectPreLaunchTask);

        // If we only want to add the configuration task, add it immediately and return early.
        if (launchConfigurationName == null) {
            // Add the task(s) to the tasks.json file and return early as we don't want to update the launch.json.
            await ThenableUtility.ToPromise(tasksConfig.update(`tasks`, tasks, vscode.ConfigurationTarget.WorkspaceFolder));
            // We have no debugConfigurationName to return.
            return null;
        }

        // If a prelaunch task already exists in the source launch configuration, create a compound task to handle it.
        const sourcePreLaunchTask: string = sourceDebugConfiguration[`preLaunchTask`];
        if (sourcePreLaunchTask != null) {
            connectPreLaunchTaskName = Constants.ConnectCompoundTaskType;
            tasks = tasks.concat({
                label: Constants.ConnectCompoundTaskType,
                dependsOn: [ Constants.ConnectResourceTaskType, sourcePreLaunchTask ],
                dependsOrder: `sequence`
            });
        }

        // Replace the Connect configuration launch configuration by the final one.
        const connectDebugConfiguration = ObjectUtility.deepCopyObject(sourceDebugConfiguration);
        const connectDebugConfigurationName = `${launchConfigurationName} with Kubernetes`;
        connectDebugConfiguration[`name`] = connectDebugConfigurationName;
        connectDebugConfiguration[`preLaunchTask`] = connectPreLaunchTaskName;
        if (connectDebugConfiguration[`env`] == null) {
            connectDebugConfiguration[`env`] = {};
            // Add this environment variable so that Bridge to Kubernetes works for c-core based GRPC clients
            // More info here https://github.com/grpc/grpc/issues/18691
            connectDebugConfiguration[`env`][`GRPC_DNS_RESOLVER`] = `native`;
        }

        // If it is a .NET core app, make sure the bindings are added for 0.0.0.0
        if (connectDebugConfiguration[`type`] === `coreclr` && ports[0] !== 0) {
            connectDebugConfiguration[`env`][`ASPNETCORE_URLS`] = ports.map(port => `http://+:${port}`).join(`;`);
        }

        // Remove from the Connect debug configuration the serverReadyAction field, as it is used to launch URLs after debugging starts,
        // and that we know that the default URL launched by the source debug configuration will not be the one useful for Connect users.
        delete connectDebugConfiguration[`serverReadyAction`];

        // Add the task(s) to the tasks.json file.
        await ThenableUtility.ToPromise(tasksConfig.update(`tasks`, tasks, vscode.ConfigurationTarget.WorkspaceFolder));

        // Make sure we add the Connect configuration at the beginning of the launch.json, so that it is selected by default once configured.
        debugConfigurations.unshift(connectDebugConfiguration);
        await ThenableUtility.ToPromise(launchConfig.update(`configurations`, debugConfigurations, vscode.ConfigurationTarget.WorkspaceFolder));

        return connectDebugConfigurationName;
    }
}