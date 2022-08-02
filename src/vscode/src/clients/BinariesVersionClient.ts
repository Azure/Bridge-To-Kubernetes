// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import fetch, { Response } from 'node-fetch';
import * as util from "util";
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { IBinariesDownloadInfo, IDownloadInfo } from '../models/IBinariesDownloadInfo';
import { Environment, EnvironmentUtility } from '../utility/EnvironmentUtility';
import { RetryUtility } from '../utility/RetryUtility';
import { ClientType } from './ClientType';

export class BinariesVersionClient {
    private _binariesDownloadInfoPromise: Promise<IBinariesDownloadInfo>;

    public constructor(
        private readonly _expectedBridgeVersion: string,
        private readonly _logger: Logger
    ) {
        this._binariesDownloadInfoPromise = null;
    }

    public async getCachedBinariesDownloadInfoAsync(): Promise<IBinariesDownloadInfo> {
        if (this._binariesDownloadInfoPromise == null) {
            this._binariesDownloadInfoPromise = this.getBinariesDownloadInfoAsync();
            this._binariesDownloadInfoPromise.then(() => {
                this._logger.trace(TelemetryEvent.BinariesVersionClient_GetCachedBinariesDownloadInfoSuccess);
            }).catch(error => {
                this._binariesDownloadInfoPromise = null;
                this._logger.error(TelemetryEvent.BinariesVersionClient_GetCachedBinariesDownloadInfoError, error);
            });
        }

        return this._binariesDownloadInfoPromise;
    }

    private async getBinariesDownloadInfoAsync(): Promise<IBinariesDownloadInfo> {
        const downloadStartTime = new Date();
        let downloadSucceeded = false;

        try {
            const getDownloadInfoAsyncFn = async (): Promise<IBinariesDownloadInfo> => {

                // Get latest download URL and checksum
                const binaryVersionsResponse: Response = await fetch(this.getBinariesVersionUrl(this._expectedBridgeVersion));
                const binaryVersionsJson: any = await binaryVersionsResponse.json();
                const osString: string = this.getOsString();
                const version: string = binaryVersionsJson[`version`];
                const binariesDownloadInfo: object = binaryVersionsJson[osString];

                const binariesDownloadInfoMap: Map<ClientType, IDownloadInfo> = new Map<ClientType, IDownloadInfo>();
                binariesDownloadInfoMap.set(ClientType.Bridge, this.getDownloadInfo(ClientType.Bridge, binariesDownloadInfo));
                binariesDownloadInfoMap.set(ClientType.Kubectl, this.getDownloadInfo(ClientType.Kubectl, binariesDownloadInfo));
                binariesDownloadInfoMap.set(ClientType.DotNet, this.getDownloadInfo(ClientType.DotNet, binariesDownloadInfo));

                downloadSucceeded = true;
                this._logger.trace(TelemetryEvent.BinariesVersionClient_GetDownloadInfoSuccess, {
                    bridgeAvailableVersion: version,
                    bridgeExpectedVersion: this._expectedBridgeVersion,
                    dotNetExpectedVersion: Constants.DotNetMinVersion,
                    kubectlExpectedVersion: Constants.KubectlMinVersion
                });

                return {
                    downloadInfoMap: binariesDownloadInfoMap
                };
            };
            return await RetryUtility.retryAsync<IBinariesDownloadInfo>(getDownloadInfoAsyncFn, /*retries*/3, /*delayInMs*/100);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BinariesVersionClient_GetDownloadInfoError, error);
            throw error;
        }
        finally {
            this._logger.trace(TelemetryEvent.BinariesVersionClient_GetDownloadInfoStatus, /*properties*/ {
                binariesVersionsDownloadTimeInMilliseconds: new Date().getTime() - downloadStartTime.getTime(),
                binariesVersionsDownloadSucceeded: downloadSucceeded
            });
        }
    }

    private getDownloadInfo(client: ClientType, binariesDownloadInfo: object): IDownloadInfo {
        return {
            downloadUrl: binariesDownloadInfo[client][`url`],
            sha256Hash: binariesDownloadInfo[client][`sha256Hash`]
        };
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

    private getBinariesVersionUrl(expectedCLIVersion: string): string {
        const environment: Environment = EnvironmentUtility.getBridgeEnvironment(this._logger);
        let versionUrl: string;
        switch (environment) {
            case Environment.Production:
                versionUrl = expectedCLIVersion === null ? Constants.BinariesLatestVersionUrlProd : util.format(Constants.BinariesVersionedUrlProd, `zipv2`, expectedCLIVersion);
                break;
            case Environment.Staging:
                versionUrl = expectedCLIVersion === null ? Constants.BinariesLatestVersionUrlStaging : util.format(Constants.BinariesVersionedUrlStaging, `zipv2`, expectedCLIVersion);
                break;
            case Environment.Dev:
                versionUrl = expectedCLIVersion === null ? Constants.BinariesLatestVersionUrlDev : util.format(Constants.BinariesVersionedUrlDev, `zipv2`, expectedCLIVersion);
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