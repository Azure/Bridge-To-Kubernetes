// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as path from 'path';

import { Constants } from "../../Constants";
import { Logger } from "../../logger/Logger";
import { TelemetryEvent } from "../../logger/TelemetryEvent";
import { AccountContextManager } from "../../models/context/AccountContextManager";
import { IBinariesDownloadInfo, IDownloadInfo } from "../../models/IBinariesDownloadInfo";
import { BinariesVersionClient } from "../BinariesVersionClient";
import { ClientType } from "../ClientType";
import { CommandRunner } from "../CommandRunner";
import { IClient } from "../IClient";
import { KubectlClient } from "../KubectlClient";
import { IClientProvider } from "./IClientProvider";

export class KubectlClientProvider implements IClientProvider {

    public constructor(
        private readonly _binaryVersionsClient: BinariesVersionClient,
        private readonly _commandRunner: CommandRunner,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _logger: Logger
    ) { }

    public Type: ClientType = ClientType.Kubectl;

    public getExecutableFilePath(): string {
        switch (process.platform) {
            case `win32`:
                return `win/kubectl.exe`;
            case `darwin`:
                return `osx/kubectl`;
            case `linux`:
                return `linux/kubectl`;
            default:
                const error = new Error(`Unsupported platform to get ${this.Type} executable path: ${process.platform}`);
                this._logger.error(TelemetryEvent.UnexpectedError, error);
                throw error;
        }
    }

    public getDownloadDirectoryName(): string {
        return Constants.KubectlDownloadDirectoryName;
    }

    public getClient(executablePath: string, dotNetPath: string = null): IClient {
        return new KubectlClient(executablePath, this._commandRunner, this._accountContextManager, this._logger);
    }

    public getExpectedVersion(): string {
        return Constants.KubectlMinVersion;
    }

    public async getDownloadInfoAsync(): Promise<IDownloadInfo> {
        const binariesDownloadInfo: IBinariesDownloadInfo = await this._binaryVersionsClient.getCachedBinariesDownloadInfoAsync();
        return binariesDownloadInfo.downloadInfoMap.get(ClientType.Kubectl);
    }

    public getLocalBuildExecutablePath(): string {
        if (process.env.BRIDGE_BUILD_PATH != null) {
            return path.join(process.env.BRIDGE_BUILD_PATH, this.getDownloadDirectoryName(), this.getExecutableFilePath());
        }

        return null;
    }

    public getExecutablesToUpdatePermissions(): string[] {
        return [ this.getExecutableFilePath() ];
    }
}