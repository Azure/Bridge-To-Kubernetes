// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as path from 'path';

import { Constants } from "../../Constants";
import { Logger } from "../../logger/Logger";
import { TelemetryEvent } from "../../logger/TelemetryEvent";
import { IBinariesDownloadInfo, IDownloadInfo } from "../../models/IBinariesDownloadInfo";
import { BinariesVersionClient } from "../BinariesVersionClient";
import { BridgeClient } from "../BridgeClient";
import { ClientType } from "../ClientType";
import { CommandRunner } from "../CommandRunner";
import { IClient } from "../IClient";
import { IClientProvider } from "./IClientProvider";

export class BridgeClientProvider implements IClientProvider {

    public constructor(
        private readonly _binaryVersionsClient: BinariesVersionClient,
        private readonly _expectedBridgeVersion: string,
        private readonly _commandRunner: CommandRunner,
        private readonly _logger: Logger
    ) { }

    public Type: ClientType = ClientType.Bridge;

    public getExecutableFilePath(): string {
        const binariesName = this._expectedBridgeVersion != null &&
                            (this._expectedBridgeVersion <= `1.0.20210708.15` ||
                            this._expectedBridgeVersion > `1.0.20210818.0`) ? `dsc` : `bridge`;

        switch (process.platform) {
        case `win32`:
            return binariesName + `.exe`;
        case `darwin`:
        case `linux`:
            return binariesName;
        default:
            const error = new Error(`Unsupported platform to get ${this.Type} executable path: ${process.platform}`);
            this._logger.error(TelemetryEvent.UnexpectedError, error);
            throw error;
        }
    }

    public getDownloadDirectoryName(): string {
        return Constants.BridgeDownloadDirectoryName;
    }

    public getClient(executablePath: string, dotNetPath: string): IClient {
        return new BridgeClient(dotNetPath, executablePath, this._commandRunner, this._expectedBridgeVersion, this._logger);
    }

    public getExpectedVersion(): string {
        return this._expectedBridgeVersion;
    }

    public async getDownloadInfoAsync(): Promise<IDownloadInfo> {
        const binariesDownloadInfo: IBinariesDownloadInfo = await this._binaryVersionsClient.getCachedBinariesDownloadInfoAsync();
        return binariesDownloadInfo.downloadInfoMap.get(ClientType.Bridge);
    }

    public getLocalBuildExecutablePath(): string {
        if (process.env.BRIDGE_BUILD_PATH != null) {
            return path.join(process.env.BRIDGE_BUILD_PATH, this.getExecutableFilePath());
        }

        return null;
    }

    public getExecutablesToUpdatePermissions(): string[] {
        return [ this.getExecutableFilePath(), path.join(`EndpointManager`, `EndpointManager`) ];
    }
}