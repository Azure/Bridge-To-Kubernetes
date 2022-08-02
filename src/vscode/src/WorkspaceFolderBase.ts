// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { Guid } from 'guid-typescript';
import * as vscode from 'vscode';

import { FileLogWriter } from './logger/FileLogWriter';
import { Logger } from './logger/Logger';

// Base class used by classes whose existence is tied to a specific workspace folder.
export abstract class WorkspaceFolderBase {
    protected readonly _uniqueId: Guid;
    protected readonly _logger: Logger;

    public constructor(
        protected readonly _context: vscode.ExtensionContext,
        protected readonly _workspaceFolder: vscode.WorkspaceFolder,
        fileLogWriter: FileLogWriter,
        logIdentifier: string) {
        this._uniqueId = Guid.create();
        this._logger = new Logger(fileLogWriter, `${logIdentifier} (${this._workspaceFolder.name}/${this._uniqueId})`);
    }
}