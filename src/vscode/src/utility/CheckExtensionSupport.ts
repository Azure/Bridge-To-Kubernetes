// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as os from 'os';
import * as vscode from 'vscode';

import { Constants } from "../Constants";
import { DebugAssetsInitializer } from '../debug/DebugAssetsInitializer';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from "../logger/TelemetryEvent";
import { EnvironmentUtility } from './EnvironmentUtility';
import { UrlUtility } from './UrlUtility';

export class CheckExtensionSupport {
    // Runs all the required steps to check if the extension can be enabled.
    // returns a callback with error message and next actions to be handled
    // by the caller.
    public static validatePrerequisites(logger: Logger, validatePostDownloadPrerequisites: boolean = false, workspaceFolder: vscode.WorkspaceFolder = null): () => void {
        const preDownloadPrerequisitesAlertCallBack = this.preDownloadValidatePrerequisites(logger);
        if (preDownloadPrerequisitesAlertCallBack != null) {
            return preDownloadPrerequisitesAlertCallBack;
        }

        if (validatePostDownloadPrerequisites) {
            const postDownloadPrerequisitesAlertCallBack = this.postDownloadValidatePrerequisites(logger, workspaceFolder);
            if (postDownloadPrerequisitesAlertCallBack != null) {
                return postDownloadPrerequisitesAlertCallBack;
            }
        }

        return null;
    }

    public static preDownloadValidatePrerequisites(logger: Logger): () => void {
        const currentOS: NodeJS.Platform = os.platform();
        if (!Constants.SupportedPlatforms.includes(currentOS)) {
            logger.warning(TelemetryEvent.Connect_UnsupportedOperatingSystemError, /*error*/ null, {
                os: currentOS
            });
            return (): void => {
                vscode.window.showWarningMessage(`The platform '${currentOS}' is currently not supported for ${Constants.ProductName}.`);
            };
        }

        return null;
    }

    public static postDownloadValidatePrerequisites(logger: Logger, workspaceFolder: vscode.WorkspaceFolder = null): () => void {
        // No need to check whether useKubernetesServiceEnvironmentVariables is set to true on Codespaces
        if (EnvironmentUtility.isCodespacesEnvironment()) {
            return null;
        }

        if (EnvironmentUtility.isRemoteEnvironment() &&
            !this.isUseKubernetesServiceEnvironmentVariablesTaskPropertyEnabled(workspaceFolder)) {
            return (): void => {
                const learnMore = `Learn More`;
                // Reading the useKubernetesServiceEnvironmentVariables property again to evaulate if the user has
                // updated its value so that a decision can be made to move onto next steps of extension activation.
                if (!this.isUseKubernetesServiceEnvironmentVariablesTaskPropertyEnabled(workspaceFolder)) {
                    vscode.window.showWarningMessage(Constants.RemoteDevelopmentLearnMoreMessage, learnMore).then((selectedValue: string) => {
                        if (selectedValue === learnMore) {
                            logger.trace(TelemetryEvent.RemoteDevelopment_LearnMoreClicked);
                            UrlUtility.openUrl(`https://aka.ms/use-k8s-svc-env-vars`);
                        }
                    });
                }
            };
        }

        return null;
    }

    private static isUseKubernetesServiceEnvironmentVariablesTaskPropertyEnabled(workspaceFolder: vscode.WorkspaceFolder): boolean {
        const tasksConfig: vscode.WorkspaceConfiguration = vscode.workspace.getConfiguration(`tasks`, workspaceFolder != null ? workspaceFolder.uri : null);
        const tasks: object[] = tasksConfig.get<{}[]>(`tasks`, /*defaultValue*/ []);

        return tasks.some((task) => {
            if (DebugAssetsInitializer.isConnectTask(task[`label`])) {
                const useKubernetesServiceEnvironmentVariablesPropertyValue: boolean = task[`useKubernetesServiceEnvironmentVariables`];
                if (useKubernetesServiceEnvironmentVariablesPropertyValue != null && useKubernetesServiceEnvironmentVariablesPropertyValue) {
                    return true;
                }
            }
            return false;
        });
    }
}