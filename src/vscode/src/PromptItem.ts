// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';

export enum PromptResult {
    Yes,
    No
}

export interface IPromptItem extends vscode.MessageItem {
    result: PromptResult;
}