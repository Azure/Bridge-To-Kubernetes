// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { FileDownloader, getApi as getFileDownloaderApi } from '@microsoft/vscode-file-downloader-api';
import * as path from 'path';
import * as process from 'process';
import * as vscode from 'vscode';

import { BinariesVersionClient } from '../clients/BinariesVersionClient';
import { BridgeClient } from '../clients/BridgeClient';
import { ClientType } from '../clients/ClientType';
import { CommandRunner } from '../clients/CommandRunner';
import { IClient } from '../clients/IClient';
import { KubectlClient } from '../clients/KubectlClient';
import { BridgeClientProvider } from '../clients/Providers/BridgeClientProvider';
import { DotNetClientProvider } from '../clients/Providers/DotNetClientProvider';
import { IClientProvider } from '../clients/Providers/IClientProvider';
import { KubectlClientProvider } from '../clients/Providers/KubectlClientProvider';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { AccountContextManager } from '../models/context/AccountContextManager';
import { IDownloadInfo } from '../models/IBinariesDownloadInfo';
import { EventSource, IReadOnlyEventSource } from '../utility/Event';
import { fileSystem } from '../utility/FileSystem';
import { VersionUtility } from '../utility/VersionUtility';
import { IBinariesUtility } from './IBinariesUtility';

export class BinariesUtilityV2 implements IBinariesUtility {
    private readonly _binaryVersionsClient: BinariesVersionClient;
    private _binariesPromise: Promise<[BridgeClient, KubectlClient]>;
    private _binariesPromiseIsResolved = false;
    private _binariesLocalStatusDeterminedPromise: Promise<void>;
    private _resolveBinariesLocalStatusDeterminedPromise: () => void;
    private _fileDownloader: FileDownloader;

    private readonly _downloadStarted: EventSource<void>;
    private readonly _downloadFinished: EventSource<void>;
    private readonly _downloadProgress: EventSource<number>;

    public constructor(
        private readonly _logger: Logger,
        private readonly _context: vscode.ExtensionContext,
        private readonly _commandEnvironmentVariables: NodeJS.ProcessEnv,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _expectedBridgeVersion: string
    ) {
        this._binaryVersionsClient = new BinariesVersionClient(this._expectedBridgeVersion, this._logger);
        this._downloadStarted = new EventSource<void>();
        this._downloadFinished = new EventSource<void>();
        this._downloadProgress = new EventSource<number>();

        this._binariesLocalStatusDeterminedPromise = new Promise<void>((resolve): void => {
            this._resolveBinariesLocalStatusDeterminedPromise = resolve;
        });
    }

    public downloadStarted(): IReadOnlyEventSource<void> {
        return this._downloadStarted;
    }

    public downloadFinished(): IReadOnlyEventSource<void> {
        return this._downloadFinished;
    }

    /**
     * Triggers once for every percent increase in the download progress.
     */
    public downloadProgress(): IReadOnlyEventSource<number> {
        return this._downloadProgress;
    }

    /**
     * If the binaries aren't downloaded, download newest versions.
     * If the binaries is already downloaded, check the version the first time.
     * If the version is insufficient, then download the newest version.
     * If the version is sufficient, then all good!
     * Returns the BridgeClient, KubectlClient to enable working with the clients.
     * Does not retry.
     */
    public async ensureBinariesAsync(): Promise<[BridgeClient, KubectlClient]> {
        this._logger.trace(`Making sure that the CLI is present locally, by downloading it if needed`);
        if (this._binariesPromise == null) {
            this._binariesPromise = this.runEnsureBinariesAsync();
            this._binariesPromise.then(() => {
                this._binariesPromiseIsResolved = true;
                this._logger.trace(TelemetryEvent.BinariesUtility_EnsureBinariesSuccess, {
                    isUsingLocalBinaries: (process.env.BRIDGE_BUILD_PATH != null).toString(),
                    isUsingLocalDotNet: (process.env.DOTNET_ROOT != null).toString()
                });
            }).catch(error => {
                this._binariesPromise = null;
                this._logger.error(TelemetryEvent.BinariesUtility_EnsureBinariesError, error, {
                    isUsingLocalBinaries: (process.env.BRIDGE_BUILD_PATH != null).toString(),
                    isUsingLocalDotNet: (process.env.DOTNET_ROOT != null).toString()
                });
            }).finally(() => {
                // Whatever happened (success or failure when getting binaries), at this
                // point we know the binaries local status.
                this._resolveBinariesLocalStatusDeterminedPromise();
            });
        }
        return this._binariesPromise;
    }

    public async tryGetBridgeAsync(): Promise<BridgeClient> {
        const clients: [BridgeClient, KubectlClient] = await this.tryGetClientsAsync();
        if (clients == null) {
            return undefined;
        }
        return clients[0];
    }

    public async tryGetKubectlAsync(): Promise<KubectlClient> {
        const clients: [BridgeClient, KubectlClient] = await this.tryGetClientsAsync();
        if (clients == null) {
            return undefined;
        }
        return clients[1];
    }

    private async tryGetClientsAsync(): Promise<[BridgeClient, KubectlClient]> {
        await this._binariesLocalStatusDeterminedPromise;
        if (this._binariesPromiseIsResolved) {
            try {
                const binaries = await this._binariesPromise;
                this._logger.trace(TelemetryEvent.BinariesUtility_TryGetBinariesSuccess);
                return binaries;
            }
            catch (error) {
                vscode.window.showErrorMessage(`This action requires dependencies that are not available yet. Please look at the "Kubernetes" item in the status bar for more information.`);
                this._logger.error(TelemetryEvent.BinariesUtility_TryGetBinariesError, error);
                return undefined;
            }
        }
        else {
            vscode.window.showErrorMessage(`This action requires dependencies that are not available yet. Please look at the "Kubernetes" item in the status bar for more information.`);
            this._logger.warning(TelemetryEvent.BinariesUtility_TryGetBinariesError);
            return undefined;
        }
    }

    /**
     * Checks if the clients, BridgeClient, dotNetClient, and kubectlClient are already downloaded.
     * For each client, if already downloaded verifies if the clients are of minimum expected version.
     * If the clients are running minimum expected version, all good!
     * If not, downloads only the clients which are not already downloaded or clients which doesn't expected version.
     * After download, performs validations on the binaries downladed.
     * Doesn't retry. The download retry is part of the filedownloader api.
     */
    private async runEnsureBinariesAsync(): Promise<[BridgeClient, KubectlClient]> {
        const bridgeClientProvider: IClientProvider = new BridgeClientProvider(this._binaryVersionsClient, this._expectedBridgeVersion, new CommandRunner(this._commandEnvironmentVariables), this._logger);
        const kubectlClientProvider: IClientProvider = new KubectlClientProvider(this._binaryVersionsClient, new CommandRunner(this._commandEnvironmentVariables), this._accountContextManager, this._logger);
        const dotNetClientProvider: IClientProvider = new DotNetClientProvider(this._binaryVersionsClient, new CommandRunner(this._commandEnvironmentVariables), this._logger);

        let bridgeClient: IClient;
        let kubectlClient: IClient;
        let dotNetClient: IClient = await this.checkIfBinaryExistsAsync(dotNetClientProvider);
        const existingDotNetPath: string = dotNetClient == null ? null : dotNetClient.getExecutablePath();
        [ bridgeClient, kubectlClient ] = await Promise.all([
            this.checkIfBinaryExistsAsync(bridgeClientProvider, existingDotNetPath),
            this.checkIfBinaryExistsAsync(kubectlClientProvider)
        ]);

        const bridgeOrKubectlClientToBeDownloaded: boolean = bridgeClient == null || kubectlClient == null;
        const numberOfBinariesToDownload = (dotNetClient == null ? 1 : 0) + (bridgeClient == null ? 1 : 0) + (kubectlClient == null ? 1 : 0);
        if (numberOfBinariesToDownload > 0) {
            // We know we need to download some binaries, and so that the local binaries are not available yet.
            this._resolveBinariesLocalStatusDeterminedPromise();
        }

        // Callback which tracks the overall download progress of the binaries
        let shouldReportDownloadProgress = true;
        const currentPercentages: number[] = [ 0, 0, 0 ];
        const overAllDownloadProgressCallBack = (binaryIndex: number, downloadPercent: number): void => {
            currentPercentages[binaryIndex] = downloadPercent;
            if (shouldReportDownloadProgress) {
                this._downloadProgress.trigger(Math.floor(currentPercentages.reduce((a, b) => a + b) / numberOfBinariesToDownload));
            }
        };

        const bridgeCleanupBeforeDownload = async (): Promise<void> => {
            await this.cleanUpBeforeDownloadAsync(bridgeClient == null ? null : bridgeClient.getExecutablePath(), existingDotNetPath, new CommandRunner(this._commandEnvironmentVariables));
        };

        const downloadStartTime: Date = new Date();
        let downloadSucceeded = false;
        let dotNetPath: string;
        let bridgePath: string;
        let kubectlPath: string;
        try {
            this._downloadStarted.trigger();
            let downloadingBinaryIndex = 0;
            const downloadBinaryPromises: Promise<string>[] = [
                dotNetClient == null ? this.downloadBinaryAsync(dotNetClientProvider, overAllDownloadProgressCallBack, downloadingBinaryIndex++)
                                    : new Promise((resolve): void => resolve(dotNetClient.getExecutablePath())),
                bridgeClient == null ? this.downloadBinaryAsync(bridgeClientProvider, overAllDownloadProgressCallBack, downloadingBinaryIndex++, bridgeCleanupBeforeDownload)
                                : new Promise((resolve): void => resolve(bridgeClient.getExecutablePath())),
                kubectlClient == null ? this.downloadBinaryAsync(kubectlClientProvider, overAllDownloadProgressCallBack, downloadingBinaryIndex++)
                                    : new Promise((resolve): void => resolve(kubectlClient.getExecutablePath()))
            ];

            [ dotNetPath, bridgePath, kubectlPath ] = await Promise.all(downloadBinaryPromises);
            downloadSucceeded = true;
        }
        catch (error) {
            shouldReportDownloadProgress = false;
            throw error;
        }
        finally {
            this._downloadFinished.trigger();
            this._logger.trace(TelemetryEvent.BinariesUtility_OverallDownloadStatus, /*properties*/ {
                numberOfBinariesDownloaded: numberOfBinariesToDownload,
                binariesDownloadTimeInMilliseconds: new Date().getTime() - downloadStartTime.getTime(),
                binariesDownloadSucceeded: downloadSucceeded
            });
        }

        // Perform validation only for the binaries that have been downloaded
        const postDownloadPromises: Promise<IClient>[] = [
            dotNetClient == null ? this.postDownloadValidationAsync(dotNetClientProvider, dotNetPath)
                                 : new Promise((resolve): void => resolve(dotNetClient)),
            bridgeClient == null ? this.postDownloadValidationAsync(bridgeClientProvider, bridgePath, dotNetPath)
                              : new Promise((resolve): void => resolve(bridgeClient)),
            kubectlClient == null ? this.postDownloadValidationAsync(kubectlClientProvider, kubectlPath)
                                  : new Promise((resolve): void => resolve(kubectlClient))
        ];

        [ dotNetClient, bridgeClient, kubectlClient ] = await Promise.all(postDownloadPromises);

        // Bridge expects kubectl to be present in its folder, so copy only if kubectl or bridge has been downloaded
        await this.moveKubectlToBridgeFolderIfRequiredAsync(bridgeOrKubectlClientToBeDownloaded, bridgePath, kubectlPath, kubectlClientProvider);

        // Clean up old 'binaries' folder after the download is complete
        if (process.env.BRIDGE_BUILD_PATH == null) {
            try {
                const bridgeDownloadDirectoryRegExp: RegExp = new RegExp(bridgeClientProvider.getDownloadDirectoryName(), `g`);
                const legacyBridgeDownloadDirectoryName: string = path.dirname(bridgePath).replace(bridgeDownloadDirectoryRegExp, Constants.LegacyBridgeDownloadDirectoryName);
                if (await fileSystem.existsAsync(legacyBridgeDownloadDirectoryName)) {
                    await fileSystem.rmdirAsync(legacyBridgeDownloadDirectoryName, { recursive: true });
                    this._logger.trace(TelemetryEvent.BinariesUtility_DeleteLegacyBridgeDownloadDirectorySuccess);
                }
            }
            catch (error) {
                this._logger.warning(TelemetryEvent.BinariesUtility_DeleteLegacyBridgeDownloadDirectoryError, error);
            }
        }

        return [ bridgeClient as BridgeClient, kubectlClient as KubectlClient ];
    }

    private async postDownloadValidationAsync(clientProvider: IClientProvider, executablePath: string, dotNetPath: string = null): Promise<IClient> {

        this._logger.trace(`Validating ${clientProvider.Type}.`);

        if (process.platform === `darwin` || process.platform === `linux`) {
            const commandRunner = new CommandRunner(this._commandEnvironmentVariables);
            const chmodPath = process.platform === `darwin` ? `/bin/chmod` : `chmod`;
            const directoryName: string = await this.getBinariesDirectoryPathAsync(clientProvider.getDownloadDirectoryName());
            const args: string[] = [ `+x` ];
            args.push(...clientProvider.getExecutablesToUpdatePermissions());
            await commandRunner.runAsync(chmodPath, args, directoryName);
        }

        const client: IClient = clientProvider.getClient(executablePath, dotNetPath);
        const expectedVersion: string = clientProvider.getExpectedVersion();
        const currentVersion: string = await client.getVersionAsync();
        if (!this.isBinaryVersionSufficient(currentVersion, expectedVersion, clientProvider.Type, clientProvider.Type === ClientType.Bridge, /*strict*/ true)) {
            const error = new Error(`Current version of the '${clientProvider.Type}' binaries does not match the expected version.`);
            this._logger.error(TelemetryEvent.UnexpectedError, error, /*properties*/ {
                expectedVersion: expectedVersion,
                actualVersion: currentVersion,
                clientType: clientProvider.Type
            });
            throw error;
        }

        return client;
    }

    /**
     * Downloads a binary using the downloadInfo properties to directory 'directoryName'.
     * Also, performs any clean up before download.
     */
    private async downloadBinaryAsync(clientProvider: IClientProvider,
                                      overAllDownloadProgressCallBack: (binaryIndex: number, downloadPercent: number) => void,
                                      downloadingBinaryIndex: number,
                                      cleanUpBeforeDownload?: () => Promise<void>
    ): Promise<string> {
        if (cleanUpBeforeDownload != null) {
            this._logger.trace(`Performing clean up before download for client '${clientProvider.Type}'`);
            await cleanUpBeforeDownload();
        }

        const downloadInfo: IDownloadInfo = await clientProvider.getDownloadInfoAsync();

        // Only increment the progress when it is greater than previous progress
        // Updates the overall download progress call back so that an aggregate progress is shown to user
        let previousProgress = 0;
        let progress = 0;
        const downloadProgressCallback = (downloadedBytes: number, totalBytes: number): void => {
            const newProgress = Math.floor(downloadedBytes / totalBytes * 100);
            if (newProgress > previousProgress) {
                previousProgress = progress;
                progress = newProgress;
                overAllDownloadProgressCallBack(downloadingBinaryIndex, progress);
            }
        };

        const downloadProperties: { [key: string]: string } = {
            clientType: clientProvider.Type,
            downloadUrl: downloadInfo.downloadUrl
        };

        // Download and make sure we can access the executable
        this._logger.trace(`Downloading client ${clientProvider.Type}...`);
        const fileDownloader: FileDownloader = await this.validateAndGetFileDownloaderApiAsync();
        let unzipDirectory: vscode.Uri;
        try {
            this._logger.trace(TelemetryEvent.BinariesUtility_DownloadStart, downloadProperties);
            unzipDirectory = await fileDownloader.downloadFile(
                vscode.Uri.parse(downloadInfo.downloadUrl),
                clientProvider.getDownloadDirectoryName(),
                this._context,
                /*cancellationToken*/ undefined,
                downloadProgressCallback,
                {
                    shouldUnzip: true,
                    retries: 8
                }
            );

            const binaryPath = path.join(unzipDirectory.fsPath, clientProvider.getExecutableFilePath());
            await fileSystem.accessAsync(binaryPath);
            this._logger.trace(TelemetryEvent.BinariesUtility_DownloadSuccess, downloadProperties);

            return binaryPath;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BinariesUtility_DownloadError, error, downloadProperties);
            throw error;
        }
    }

    /*
    * Checks if the binary at the executableFile is accessible and is of minimum expected version
    * If the binary satisfies the above conditions returns an instance of the client.
    * If not, returns null which signifies that a download should happen.
    */
    private async checkIfBinaryExistsAsync(clientProvider: IClientProvider, dotNetPath: string = null): Promise<IClient> {

        const localBuildExecutablePath: string = clientProvider.getLocalBuildExecutablePath();

        if (localBuildExecutablePath != null) {
            const client = clientProvider.getClient(localBuildExecutablePath, dotNetPath);
            this._logger.trace(`The local build environment variable is set for '${clientProvider.Type}': targeting the binary located at ${localBuildExecutablePath}`);
            return client;
        }

        // Bridge Client depends on .Net Runtime, if dotNet is not available
        // we cannot run bridge commands, so expect to download bridge client
        if (clientProvider.Type === ClientType.Bridge && dotNetPath == null) {
            return null;
        }

        const properties: { [key: string]: string } = {
            clientType: clientProvider.Type
        };

        let clientExecutablePath: string = null;
        try {
            clientExecutablePath = await this.getBinaryExecutablePathAsync(clientProvider);
        }
        catch (error) {
            // Most likely, the binaries are downloaded locally but are corrupted. Keeping the
            // clientExecutablePath == null so that we redownload the files.
            this._logger.error(TelemetryEvent.BinariesUtility_CheckIfBinaryExistsError, error, properties);
        }

        if (clientExecutablePath == null) {
            this._logger.warning(`${clientProvider.Type} is not present locally.`);
            return null;
        }

        let client: IClient;
        let currentClientVersion: string;
        try {
            client = clientProvider.getClient(clientExecutablePath, dotNetPath);
            currentClientVersion = await client.getVersionAsync();
        }
        catch (error) {
            this._logger.warning(`Encountered error running get version on ${clientProvider.Type}: ${error.message}`, /*error*/null, properties);
            return null;
        }

        const expectedVersion = clientProvider.getExpectedVersion();

        if (!this.isBinaryVersionSufficient(currentClientVersion, expectedVersion, clientProvider.Type, /*allowLocalBuildFormat*/ clientProvider.Type === ClientType.Bridge)) {
            this._logger.warning(`${clientProvider.Type} binaries are present locally but their version is outdated: downloading the latest ones.`, /*error*/ null, /*properties*/ {
                currentClientVersion: currentClientVersion,
                expectedVersion: expectedVersion,
                clientType: clientProvider.Type
            });

            return null;
        }

        return Promise.resolve(client);
    }

    private async moveKubectlToBridgeFolderIfRequiredAsync(bridgeOrKubectlClientDownloaded: boolean,
                                                           bridgePath: string,
                                                           kubectlPath: string,
                                                           kubectlClientProvider: IClientProvider
    ): Promise<void> {
        try {
            const kubectlExecutableFilePathForBridge: string = path.join(path.dirname(bridgePath), kubectlClientProvider.getDownloadDirectoryName(), kubectlClientProvider.getExecutableFilePath());
            const checkKubectlExistsWithRequiredVersionCallBack = async (): Promise<boolean> => {
                if (await fileSystem.existsAsync(kubectlExecutableFilePathForBridge)) {
                    const kubectlClient: IClient = kubectlClientProvider.getClient(kubectlExecutableFilePathForBridge, /*dotNetPath*/ null);
                    const kubectlVersion: string = await kubectlClient.getVersionAsync();
                    if (this.isBinaryVersionSufficient(kubectlVersion, kubectlClientProvider.getExpectedVersion(), kubectlClientProvider.Type, /*allowLocalBuildFormat*/ false)) {
                        return true;
                    }
                }
                return false;
            };
            if (bridgeOrKubectlClientDownloaded || !await checkKubectlExistsWithRequiredVersionCallBack()) {
                await fileSystem.mkdirAsync(path.dirname(kubectlExecutableFilePathForBridge), { recursive: true });
                await fileSystem.copyFileAsync(kubectlPath, kubectlExecutableFilePathForBridge);
                this._logger.trace(TelemetryEvent.BinariesUtility_CopyKubectlToBridgeFolderSuccess);
            }
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BinariesUtility_CopyKubectlToBridgeFolderError, error);
            throw new Error(`Failed to move ${ClientType.Kubectl} to ${ClientType.Bridge} folder with error: ${error.message}`);
        }
    }

    // Performs any clean up before the binaries are downloaded.
    // Currently performs `bridge clean-local-connect`
    private async cleanUpBeforeDownloadAsync(bridgeExecutablePath: string, dotNetPath: string, commandRunner: CommandRunner): Promise<void> {
        try {
            if (bridgeExecutablePath == null || dotNetPath == null) {
                return;
            }
            const oldBridgeClient = new BridgeClient(dotNetPath, bridgeExecutablePath, commandRunner, this._expectedBridgeVersion, this._logger);
            await oldBridgeClient.cleanLocalConnectAsync();
            this._logger.trace(TelemetryEvent.BinariesUtility_CleanUpBeforeDownloadSuccess, /*properties*/ {
                clientType: ClientType.Bridge
            });
        }
        catch (error) {
            this._logger.warning(`Error occured while performing clean up before download. Error: ${error.message}`);
            this._logger.error(TelemetryEvent.BinariesUtility_CleanUpBeforeDownloadError, error, /*properties*/ {
                clientType: ClientType.Bridge
            });
        }
    }

    /** Return the path to the binary executable if it exists, undefined if it doesn't */
    private async getBinaryExecutablePathAsync(clientProvider: IClientProvider): Promise<string | undefined> {
        const binariesDirectoryPath: string = await this.getBinariesDirectoryPathAsync(clientProvider.getDownloadDirectoryName());
        if (binariesDirectoryPath == null) {
            return undefined;
        }
        const executablePath = path.join(binariesDirectoryPath, clientProvider.getExecutableFilePath());
        await fileSystem.accessAsync(executablePath);
        this._logger.trace(`Successfully determined the ${clientProvider.Type} binary path: ${executablePath}`);
        return executablePath;
    }

    /** Return the path to the binaries directory if it exists, undefined if it doesn't */
    private async getBinariesDirectoryPathAsync(directoryName: string): Promise<string | undefined> {
        const fileDownloader: FileDownloader = await this.validateAndGetFileDownloaderApiAsync();
        const unzipDirectory: vscode.Uri = await fileDownloader.tryGetItem(directoryName, this._context);
        if (unzipDirectory == null) {
            return undefined;
        }
        return unzipDirectory.fsPath;
    }

    /** Returns true if the current binary version is greater than or equal to the minimum required version.
     * If strict is set to true, only returns true if the versions are strictly equal.
     */
    private isBinaryVersionSufficient(currentVersion: string, expectedVersion: string, clientType: ClientType, allowLocalBuildFormat: boolean, strict: boolean = false): boolean {
        try {
            this._logger.trace(`Found local ${clientType} version: '${currentVersion}'. Minimum expected version: '${expectedVersion}'`);
            const isBinaryVersionSufficient = expectedVersion == null || VersionUtility.isVersionSufficient(currentVersion, expectedVersion, allowLocalBuildFormat, strict);
            if (isBinaryVersionSufficient) {
                this._logger.trace(`Local ${clientType} has version number '${currentVersion}', which is greater than or equal to the minimum requirement '${expectedVersion}'`);
            }
            else {
                this._logger.trace(`Local ${clientType} has version number '${currentVersion}', which is smaller than the minimum requirement '${expectedVersion}'`);
            }

            return isBinaryVersionSufficient;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.UnexpectedError, new Error(`Failed to retrieve or parse the ${clientType} version: ${error.message}`));
            throw error;
        }
    }

    private async validateAndGetFileDownloaderApiAsync(): Promise<FileDownloader> {
        if (this._fileDownloader == null) {
            const fileDownloaderExtension = this.getFileDownloaderExtension();
            if (!VersionUtility.isVersionSufficient(fileDownloaderExtension.packageJSON[`version`], Constants.FileDownloaderMinVersion)) {
                const error = new Error(Constants.FileDownloaderVersionError);
                this._logger.error(TelemetryEvent.BinariesUtility_FileDownloaderVersionError, error);
                throw error;
            }

            this._fileDownloader = await getFileDownloaderApi();
        }

        return this._fileDownloader;
    }

    private getFileDownloaderExtension(): vscode.Extension<any> {
        const fileDownloaderExtension: any = vscode.extensions.getExtension(`mindaro-dev.file-downloader`);
        if (fileDownloaderExtension == null) {
            const error = new Error(`${Constants.ProductName} cannot run without the File downloader extension. Please install the extension manually.`);
            this._logger.error(TelemetryEvent.UnexpectedError, error);
            throw error;
        }
        return fileDownloaderExtension;
    }
}