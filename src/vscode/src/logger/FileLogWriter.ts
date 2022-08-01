// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as fs from 'fs';
import * as path from 'path';
import * as vscode from 'vscode';

import { fileSystem } from '../utility/FileSystem';

export class FileLogWriter {
    private readonly LogFileNameBase = `mindaro-vscode`;
    private readonly LogFileNameExtension = `txt`;
    private readonly MaxLastLogsLength = 50;

    private readonly _logDirectoryPath: string;
    private readonly _lastLogs: string[];
    private _logWriteStream: fs.WriteStream;
    private _writePromise: Promise<void>;

    public constructor(context: vscode.ExtensionContext) {
        this._logDirectoryPath = context.logPath;
        this._writePromise = Promise.resolve();
        this._lastLogs = [];
    }

    public logFilePath: string;

    public async initializeAsync(): Promise<void> {
        this.logFilePath = await this.getLogFilePathAsync(this._logDirectoryPath);
        // Get a stream on the file as "Appending - File is created if it does not exist".
        this._logWriteStream = fs.createWriteStream(this.logFilePath, { flags: `a`, autoClose: false });
        this._logWriteStream.on(`error`, (error: Error) => {
            // Something went wrong while writing logs. Ignoring.
        });
    }

    public write(message: string): void {
        this._writePromise = this._writePromise.then(async () => {
            this._lastLogs.push(message);
            if (this._lastLogs.length > this.MaxLastLogsLength) {
                this._lastLogs.shift();
            }

            return new Promise<void>((resolve): void => {
                this._logWriteStream.write(message, (error: Error) => {
                    if (error != null) {
                        // Something went wrong while writing logs. Ignoring.
                    }
                    resolve();
                });
            });
        });
    }

    public async closeAsync(): Promise<void> {
        await this._writePromise;
        this._logWriteStream.end();
    }

    // Returns the last logs stored, so that we can attach them to telemetry in case of errors.
    public async getLastLogsAsync(): Promise<string> {
        return this._lastLogs.join(``);
    }

    private async getLogFilePathAsync(logDirectoryPath: string): Promise<string> {
        // Create log directory if needed.
        try {
            await fileSystem.mkdirAsync(logDirectoryPath);
        }
        catch (error) {
            if (error.code !== `EEXIST`) {
                // If we get any other error than "already existing folder", let's throw.
                throw error;
            }
        }

        const logFileName = `${this.LogFileNameBase}-${new Date().toISOString().replace(/:/g, `-`)}.${this.LogFileNameExtension}`;
        return path.join(logDirectoryPath, logFileName);
    }
}