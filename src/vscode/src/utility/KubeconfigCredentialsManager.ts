// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';
import { BridgeClient } from '../clients/BridgeClient';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { IAuthenticationTarget } from '../models/IAuthenticationTarget';

export class KubeconfigCredentialsManager {
    public static async refreshCredentialsAsync(kubeconfigPath: string, namespace: string, bridgeClient: BridgeClient, logger: Logger): Promise<boolean> {
        const startTime = Date.now();
        let refreshWasNecessary = false;
        let result = false;
        try {
            await bridgeClient.refreshCredentialsAsync(kubeconfigPath, namespace, (authenticationTarget: IAuthenticationTarget) => {
                logger.trace(TelemetryEvent.KubeConfigCredentialsManager_RefreshCredentialsNecessary);
                vscode.window.showErrorMessage(`The kubeconfig token needs to be refreshed. To sign in, use a web browser to open the page [${authenticationTarget.url}](${authenticationTarget.url}) and enter the code ${authenticationTarget.authenticationCode} to authenticate.`);
                refreshWasNecessary = true;
            });
            if (refreshWasNecessary) {
                logger.trace(TelemetryEvent.KubeConfigCredentialsManager_RefreshCredentialsSuccess);
                vscode.window.showInformationMessage(`The token was refreshed successfully.`);
            }
            result = true;
        }
        catch (error) {
            logger.error(TelemetryEvent.KubeConfigCredentialsManager_RefreshCredentialsError, error);
            vscode.window.showErrorMessage(`Failed to refresh the kubeconfig token: ${error.message}`);
            result = false;
        }
        finally {
            const endTime = Date.now();
            logger.trace(TelemetryEvent.KubeConfigCredentialsManager_RefreshCredentialsPerf, {
                success: result,
                refreshRequired: refreshWasNecessary,
                durationMs: endTime - startTime
            });
        }
        return result;
    }

    public static async checkCredentialsAsync(kubeconfigPath: string, namespace: string, bridgeClient: BridgeClient, logger: Logger): Promise<boolean> {
        const startTime = Date.now();
        let result = false;
        try {
            await bridgeClient.checkCredentialsAsync(kubeconfigPath, namespace);
            result = true;
        }
        catch (error) {
            result = false;
        }
        finally {
            const endTime = Date.now();
            logger.trace(TelemetryEvent.KubeConfigCredentialsManager_CheckCredentialsPerf, {
            success: result,
            durationMs: endTime - startTime
            });
        }
        return result;
    }
}