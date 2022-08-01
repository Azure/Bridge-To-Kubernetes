// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';
import { Constants } from '../Constants';

import { Logger } from "../logger/Logger";
import { TelemetryEvent } from "../logger/TelemetryEvent";

export enum Environment {
    Production = "production",
    Staging = "staging",
    Dev = "dev"
}

export class EnvironmentUtility {
    public static getBridgeEnvironment(logger: Logger): Environment {
        if (process.env.BRIDGE_ENVIRONMENT == null) {
            return Environment.Production;
        }

        const environment = process.env.BRIDGE_ENVIRONMENT.toLowerCase();
        switch (environment) {
            case `prod`:
            case `production`:
                return Environment.Production;
            case `staging`:
            case `stage`:
                return Environment.Staging;
            case `dev`:
            case `development`:
                return Environment.Dev;
            default:
                const error = new Error(`Unsupported value for the BRIDGE_ENVIRONMENT environment variable: ${environment}`);
                logger.error(TelemetryEvent.UnexpectedError, error);
                throw error;
        }
    }

    public static isRemoteEnvironment(): boolean {
        const bridgeToKubernetesExtension: vscode.Extension<any> = vscode.extensions.getExtension(Constants.ExtensionIdentifier);
        if (bridgeToKubernetesExtension != null && bridgeToKubernetesExtension.extensionKind === vscode.ExtensionKind.Workspace) {
            return true;
        }

        return false;
    }

    public static isCodespacesEnvironment(): boolean {
        return vscode.env.uiKind === vscode.UIKind.Web;
    }
}