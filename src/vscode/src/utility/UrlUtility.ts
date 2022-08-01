// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as opener from 'opener';
import * as vscode from 'vscode';

import { EnvironmentUtility } from "./EnvironmentUtility";

export class UrlUtility {
    public static openUrl(url: string): void {
        if (url == null) {
            throw new Error(`Cannot open the invalid url.`);
        }

        if (EnvironmentUtility.isRemoteEnvironment() || EnvironmentUtility.isCodespacesEnvironment()) {
            vscode.commands.executeCommand(`vscode.open`, vscode.Uri.parse(url));
        }
        else {
            opener(url);
        }
    }
}