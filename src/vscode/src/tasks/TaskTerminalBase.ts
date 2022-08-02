// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';

import { Constants } from '../Constants';

export abstract class TaskTerminalBase implements vscode.Pseudoterminal {
    private readonly _writeEmitter = new vscode.EventEmitter<string>();
    private readonly _closeEmitter = new vscode.EventEmitter<number>();

    public onDidWrite: vscode.Event<string> = this._writeEmitter.event;
    public onDidClose?: vscode.Event<number> = this._closeEmitter.event;

    public abstract async open(initialDimensions: vscode.TerminalDimensions | undefined): Promise<void>;

    public close(): void {
        // The terminal has been closed by the user. Nothing specific to do.
    }

    // Normalize line endings in multi-line text (e.g. logs from the cluster), and output to the terminal
    protected _write(text: string): void {
        text = text.split(`\n`).join(Constants.PseudoterminalNewLine);
        this._writeEmitter.fire(text);
    }

    // Output text followed by a newline
    protected _writeLine(text: string): void {
        this._write(text + Constants.PseudoterminalNewLine);
    }

    // Close the terminal with an exit code indicating success
    protected _exit(code: number): void {
        this._closeEmitter.fire(code);
    }
}