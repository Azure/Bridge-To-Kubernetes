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
import { ClientType } from "../ClientType";
import { CommandRunner } from "../CommandRunner";
import { DotNetClient } from "../DotNetClient";
import { IClient } from "../IClient";
import { IClientProvider } from "./IClientProvider";

export class DotNetClientProvider implements IClientProvider {

    public constructor(
      private readonly _binaryVersionsClient: BinariesVersionClient,
      private readonly _commandRunner: CommandRunner,
      private readonly _logger: Logger
    ) { }

    public Type: ClientType = ClientType.DotNet;

    public getExecutableFilePath(): string {
        switch (process.platform) {
          case `win32`:
              return `dotnet.exe`;
          case `darwin`:
          case `linux`:
              return `dotnet`;
          default:
              const error = new Error(`Unsupported platform to get ${this.Type} executable path: ${process.platform}`);
              this._logger.error(TelemetryEvent.UnexpectedError, error);
              throw error;
        }
    }

    public getDownloadDirectoryName(): string {
        return Constants.DotNetDownloadDirectoryName;
    }

    public getClient(executablePath: string, dotNetPath: string = null): IClient {
        return new DotNetClient(executablePath, this._commandRunner, this._logger);
    }

    public getExpectedVersion(): string {
        return Constants.DotNetMinVersion;
    }

    public async getDownloadInfoAsync(): Promise<IDownloadInfo> {
        const binariesDownloadInfo: IBinariesDownloadInfo = await this._binaryVersionsClient.getCachedBinariesDownloadInfoAsync();
        return binariesDownloadInfo.downloadInfoMap.get(ClientType.DotNet);
    }

    public getLocalBuildExecutablePath(): string {
      if (process.env.DOTNET_ROOT != null) {
          return path.join(process.env.DOTNET_ROOT, this.getExecutableFilePath());
      }

      return null;
    }

    public getExecutablesToUpdatePermissions(): string[] {
      return [ this.getExecutableFilePath() ];
    }
}