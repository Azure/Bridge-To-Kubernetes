// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';

import { ExtensionRoot } from './ExtensionRoot';

let _extensionRoot: ExtensionRoot;

// ExtensionHost surfaces the API expected by the VS Code extension API.
// This is the first point of entry in our extension.
export async function activate(context: vscode.ExtensionContext): Promise<void> {
    _extensionRoot = new ExtensionRoot();
    await _extensionRoot.activateAsync(context);
}

export async function deactivate(): Promise<void> {
    await _extensionRoot.deactivateAsync();
}