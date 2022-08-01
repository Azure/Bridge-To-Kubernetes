// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';
import * as path from 'path';

import { ClientType } from '../clients/ClientType';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { RetryUtility } from '../utility/RetryUtility';
import { CommandRunner } from './CommandRunner';
import { IClient } from './IClient';

export class DotNetClient implements IClient {
    public constructor(
        private readonly _executablePath: string,
        private readonly _commandRunner: CommandRunner,
        private readonly _logger: Logger) {
        }

    public readonly Type: ClientType = ClientType.DotNet;

    public async getVersionAsync(): Promise<string> {
        try {
            const getVersionAsyncFn = async (): Promise<string> => {
                const args: string[] = [ `--list-runtimes` ];
                const output: string = await this._commandRunner.runAsync(this._executablePath, args);
                const dotNetRuntimeInfo: string = output.trim();
                const dotNetVersion = this.parseVersionFromRuntimeInfo(dotNetRuntimeInfo);
                this._logger.trace(TelemetryEvent.DotNetClient_GetVersionSuccess);
                return dotNetVersion;
            };
            return await RetryUtility.retryAsync<string>(getVersionAsyncFn, /*retries*/3, /*delayInMs*/100);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.DotNetClient_GetVersionError, error);
            throw new Error(`Failed to retrieve dotnet runtime version: ${error.message}`);
        }
    }

    public getExecutablePath(): string {
        return this._executablePath;
    }

    // Runtime info can be of following lines
    // Microsoft.NETCore.App 3.1.6 [.../shared/Microsoft.NETCore.App]
    // Microsoft.NETCore.App 2.2.1 [.../shared/Microsoft.NETCore.App]
    // Microsoft.AspNetCore.All 2.1.2 [.../shared/Microsoft.AspNetCore.All]
    private parseVersionFromRuntimeInfo(dotNetRuntimeInfo: string): string {
        const dotNetCoreAppIdentifier = `Microsoft.NETCore.App`;
        const dotNetDownloadDirectory: string = path.dirname(this._executablePath);
        let dotNetCoreAppPath: string = dotNetDownloadDirectory.concat(path.sep, `shared`, path.sep, dotNetCoreAppIdentifier);
        const replacePathSeperatorRegExp: RegExp = new RegExp(`\\` + path.sep, `g`);

        // Replacing, '\' with '_' for regExp to work properly on paths.
        dotNetCoreAppPath = dotNetCoreAppPath.replace(replacePathSeperatorRegExp, `_`);
        dotNetRuntimeInfo = dotNetRuntimeInfo.replace(replacePathSeperatorRegExp, `_`);

        // Exclude the part of the path before our extension, as it might contain non-ASCII characters
        // that we can't compare to the output of dotnet --list-runtimes.
        dotNetCoreAppPath = dotNetCoreAppPath.substring(dotNetCoreAppPath.indexOf(`_${Constants.ExtensionIdentifier}_`));

        const dotNetPathRegExp: RegExp = new RegExp(`.*${dotNetCoreAppPath}`, `g`);
        const dotNetCoreRuntimes: string[] = dotNetRuntimeInfo.match(dotNetPathRegExp);

        if (dotNetCoreRuntimes == null || dotNetCoreRuntimes.length !== 1) {
            const errorMsg = dotNetCoreRuntimes == null ? `Didn't find .Net Core runtime` : `Found more than one .Net Core runtimes`;
            const error = new Error(`${errorMsg} at ${dotNetDownloadDirectory}.`);
            this._logger.error(TelemetryEvent.UnexpectedError, error);
            throw error;
        }

        // First replace the path in '[]' and then replace 'Microsoft.NETCore.App' from the remaining string
        // to extract version
        dotNetCoreRuntimes[0] = dotNetCoreRuntimes[0].replace(/\[.*/g, ``).replace(dotNetCoreAppIdentifier, ``).trim();
        return dotNetCoreRuntimes[0];
    }
}