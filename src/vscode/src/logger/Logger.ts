// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { Fault, FaultType, Telemetry } from 'telaug';
import * as vscode from 'vscode';

import { Constants } from '../Constants';
import { EnvironmentUtility } from '../utility/EnvironmentUtility';
import { FileLogWriter } from './FileLogWriter';
import { LogLevel } from './LogLevel';
import { TelemetryEvent } from './TelemetryEvent';

export class Logger {
    private static readonly SourceIdentifierMaxLength: number = 24;
    private readonly _defaultProperties: { [key: string]: string } = {};

    public constructor(private readonly _fileLogWriter: FileLogWriter, private readonly _sourceIdentifier: string) {
        this.logFilePath = this._fileLogWriter.logFilePath;

        if (EnvironmentUtility.isRemoteEnvironment()) {
            this._defaultProperties[`remoteName`] = vscode.env.remoteName;
        }

        if (EnvironmentUtility.isCodespacesEnvironment()) {
            this._defaultProperties[`isCodespaces`] = `true`;
        }
    }

    public readonly logFilePath: string;

    public trace(event: string | TelemetryEvent, properties?: { [key: string]: string | number | boolean }, error?: Error): void {
        this.log(LogLevel.Trace, event, properties, error);
    }

    public warning(event: string | TelemetryEvent, error?: Error, properties?: { [key: string]: string | number | boolean }): void {
        this.log(LogLevel.Warning, event, properties, error);
    }

    public error(event: TelemetryEvent, error: Error, properties?: { [key: string]: string | number | boolean }): void {
        this.log(LogLevel.Error, event, properties, error);
    }

    public async closeAsync(): Promise<void> {
        await this._fileLogWriter.closeAsync();
    }

    public setSharedProperty(name: string, value: string): void {
        Telemetry.addContextProperty(name, value);
    }

    private log(level: LogLevel, eventName: string | TelemetryEvent, properties?: { [key: string]: string | number | boolean }, error?: Error): void {
        const messages: string[] = [];
        properties = properties == null ? this._defaultProperties : { ...properties, ...this._defaultProperties };

        // Log telemetry if the event passed is of type TelemetryEvent.
        if ((Object.values(TelemetryEvent) as string[]).includes(eventName)) {
            if (error == null) {
                // Make sure that all values are stringified.
                let stringifiedProperties: { [key: string]: string } = null;
                if (properties != null) {
                    stringifiedProperties = {};
                    for (const key of Object.keys(properties)) {
                        const propertyValue: string | number | boolean = properties[key];
                        stringifiedProperties[key] = (propertyValue != null && typeof propertyValue !== `string` ? propertyValue.toString() : propertyValue as string);
                    }
                }

                Telemetry.sendTelemetryEvent(eventName, stringifiedProperties);
                messages.push(`Event: ${eventName}`);
            }
            else {
                // TODO: Replace by Telemetry.sendFault once it supports properties.
                const fault = new Fault(eventName, FaultType.Error, error.message, error);
                if (properties != null) {
                    for (const key of Object.keys(properties)) {
                        fault.addProperty(key, properties[key]);
                    }
                }
                fault.send();

                messages.push(`Error: ${eventName}`);
            }
        }
        else {
            messages.push(eventName);
        }

        // Log an entry in the file log.
        if (properties != null) {
            messages.push(`<json>${this.cleanLineJumps(JSON.stringify(properties), /*replaceWith*/ ``)}</json>`);
        }

        if (error != null) {
            if (error.stack != null) {
                messages.push(`<stack>${this.cleanLineJumps(error.stack)}</stack>`);
            }
            else {
                messages.push(error.toString());
            }
        }

        const dateTime: string = new Date().toISOString();
        const formattedMessage = `${dateTime} | ${this._sourceIdentifier.slice(0, Logger.SourceIdentifierMaxLength).padEnd(Logger.SourceIdentifierMaxLength)} | ${level} | ${messages.join(` `)}\n`;
        this._fileLogWriter.write(formattedMessage);
    }

    private cleanLineJumps(message: string, replaceWith: string = `\\n`): string {
        return message != null ? message.replace(/(?:\r\n|\r|\n)/g, replaceWith) : ``;
    }
}