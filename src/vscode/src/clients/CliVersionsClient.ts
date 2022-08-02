// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import fetch, { Response } from 'node-fetch';
import * as util from "util";
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { ICliDownloadInfo } from '../models/ICliDownloadInfo';
import { Environment, EnvironmentUtility } from '../utility/EnvironmentUtility';
import { RetryUtility } from '../utility/RetryUtility';

export class CliVersionsClient {
    public constructor(
        private readonly _logger: Logger
    ) { }

    public async getDownloadInfoAsync(expectedCLIVersion: string): Promise<ICliDownloadInfo> {
        const downloadStartTime = new Date();
        let downloadSucceeded = false;

        try {
            const getDownloadInfoAsyncFn = async (): Promise<ICliDownloadInfo> => {
                // Get latest download URL and checksum
                const cliVersionsResponse: Response = await fetch(this.getCliVersionsUrl(expectedCLIVersion));
                const cliVersionsJson: any = await cliVersionsResponse.json();
                const osString: string = this.getOsString();
                const version: string = cliVersionsJson[`version`];
                const downloadInfo: object = cliVersionsJson[osString];

                downloadSucceeded = true;
                this._logger.trace(TelemetryEvent.CliVersionsClient_GetDownloadInfoSuccess, {
                    availableVersion: version,
                    expectedVersion: expectedCLIVersion
                });
                return {
                    downloadUrl: downloadInfo[`url`],
                    sha256Hash: downloadInfo[`sha256Hash`]
                };
            };
            return await RetryUtility.retryAsync<ICliDownloadInfo>(getDownloadInfoAsyncFn, /*retries*/3, /*delayInMs*/100);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.CliVersionsClient_GetDownloadInfoError, error);
            throw error;
        }
        finally {
            this._logger.trace(TelemetryEvent.CliVersionsClient_GetDownloadInfoStatus, /*properties*/ {
                binariesVersionsDownloadTimeInMilliseconds: new Date().getTime() - downloadStartTime.getTime(),
                binariesVersionsDownloadSucceeded: downloadSucceeded
            });
        }
    }

    private getOsString(): string {
        switch (process.platform) {
            case `win32`:
                return `win`;
            case `darwin`:
                return `osx`;
            case `linux`:
                return `linux`;
            default:
                const error = new Error(`Unsupported platform: ${process.platform}`);
                this._logger.error(TelemetryEvent.UnexpectedError, error);
                throw error;
        }
    }

    private getCliVersionsUrl(expectedCLIVersion: string): string {
        const environment: Environment = EnvironmentUtility.getBridgeEnvironment(this._logger);
        let versionUrl: string;
        switch (environment) {
            case Environment.Production:
                versionUrl = expectedCLIVersion === null ? Constants.CliVersionsUrlProd : util.format(Constants.BinariesVersionedUrlProd, `zip`, expectedCLIVersion);
                break;
            case Environment.Staging:
                versionUrl = expectedCLIVersion === null ? Constants.CliVersionsUrlStaging : util.format(Constants.BinariesVersionedUrlStaging, `zip`, expectedCLIVersion);
                break;
            case Environment.Dev:
                versionUrl = expectedCLIVersion === null ? Constants.CliVersionsUrlDev : util.format(Constants.BinariesVersionedUrlDev, `zip`, expectedCLIVersion);
                break;
            default:
                const error = new Error(`Unsupported value for the Environment enum: ${environment}`);
                this._logger.error(TelemetryEvent.UnexpectedError, error);
                throw error;
        }
        this._logger.trace(`Resolved versionUrl '${versionUrl}'`);
        return versionUrl;
    }
}