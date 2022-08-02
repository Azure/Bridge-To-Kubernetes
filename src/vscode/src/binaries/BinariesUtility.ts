// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { FileDownloader, getApi as getFileDownloaderApi } from '@microsoft/vscode-file-downloader-api';
import * as path from 'path';
import * as process from 'process';
import * as vscode from 'vscode';

import { BridgeClient as CliClient } from '../clients/BridgeClient';
import { CliVersionsClient } from '../clients/CliVersionsClient';
import { CommandRunner } from '../clients/CommandRunner';
import { KubectlClient } from '../clients/KubectlClient';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { AccountContextManager } from '../models/context/AccountContextManager';
import { ICliDownloadInfo } from '../models/ICliDownloadInfo';
import { EventSource, IReadOnlyEventSource } from '../utility/Event';
import { fileSystem } from '../utility/FileSystem';
import { VersionUtility } from '../utility/VersionUtility';
import { IBinariesUtility } from './IBinariesUtility';

export class BinariesUtility implements IBinariesUtility {
    private readonly _cliVersionsClient: CliVersionsClient;
    private _binariesPromise: Promise<[CliClient, KubectlClient]>;
    private _binariesPromiseIsResolved = false;
    private _binariesLocalStatusDeterminedPromise: Promise<void>;
    private _resolveBinariesLocalStatusDeterminedPromise: () => void;

    private readonly _downloadStarted: EventSource<void>;
    private readonly _downloadFinished: EventSource<void>;
    private readonly _downloadProgress: EventSource<number>;

    public constructor(
        private readonly _logger: Logger,
        private readonly _context: vscode.ExtensionContext,
        private readonly _commandEnvironmentVariables: NodeJS.ProcessEnv,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _expectedCLIVersion: string
    ) {
        this._cliVersionsClient = new CliVersionsClient(this._logger);
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
     * If the CLI isn't downloaded, download the newest version.
     * If the CLI is already downloaded, check the version the first time.
     * If the version is insufficient, then download the newest version.
     * If the version is sufficient, then all good!
     * Returns the CliClient used to communicate with this CLI.
     * Does not retry.
     */
    public async ensureBinariesAsync(): Promise<[CliClient, KubectlClient]> {
        this._logger.trace(`Making sure that the CLI is present locally, by downloading it if needed`);
        if (this._binariesPromise == null) {
            this._binariesPromise = this.runEnsureBinariesAsync();
            this._binariesPromise.then(() => {
                this._binariesPromiseIsResolved = true;
                this._logger.trace(TelemetryEvent.BinariesUtility_EnsureCliSuccess, {
                    isUsingLocalBinaries: (process.env.BRIDGE_BUILD_PATH != null).toString()
                });
            }).catch(error => {
                this._binariesPromise = null;
                this._logger.error(TelemetryEvent.BinariesUtility_EnsureCliError, error, {
                    isUsingLocalBinaries: (process.env.BRIDGE_BUILD_PATH != null).toString()
                });
            });
        }
        return this._binariesPromise;
    }

    public async tryGetBridgeAsync(): Promise<CliClient> {
        const clients: [CliClient, KubectlClient] = await this.tryGetClientsAsync();
        if (clients == null) {
            return undefined;
        }
        return clients[0];
    }

    public async tryGetKubectlAsync(): Promise<KubectlClient> {
        const clients: [CliClient, KubectlClient] = await this.tryGetClientsAsync();
        if (clients == null) {
            return undefined;
        }
        return clients[1];
    }

    private async tryGetClientsAsync(): Promise<[CliClient, KubectlClient]> {
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

    private async runEnsureBinariesAsync(): Promise<[CliClient, KubectlClient]> {
        // If we specify a path to a local build of the CLI, then use it and ignore the version.
        if (process.env.BRIDGE_BUILD_PATH != null) {
            const cliExecutablePath = path.join(process.env.BRIDGE_BUILD_PATH, this.cliExecutableFilePath);
            const kubectlExecutablePath = path.join(process.env.BRIDGE_BUILD_PATH, this.kubectlExecutableFilePath);
            const cliClient = new CliClient(/*dotNetPath*/null, cliExecutablePath, new CommandRunner(this._commandEnvironmentVariables), this._expectedCLIVersion, this._logger);
            const kubectlClient = new KubectlClient(kubectlExecutablePath, new CommandRunner(this._commandEnvironmentVariables), this._accountContextManager, this._logger);
            this._logger.warning(`The BRIDGE_BUILD_PATH environment variable is set: targeting the binaries located at ${process.env.BRIDGE_BUILD_PATH}`);
            this._resolveBinariesLocalStatusDeterminedPromise();
            return [ cliClient, kubectlClient ];
        }

        let cliExecutablePath: string = null;
        let kubectlExecutablePath: string = null;
        try {
            cliExecutablePath = await this.getCliExecutablePathAsync();
            kubectlExecutablePath = await this.getKubectlExecutablePathAsync();
        }
        catch (error) {
            // Most likely, the binaries are downloaded locally but are corrupted. Keeping the
            // cliExecutablePath/kubectlExecutablePath == null so that we redownload the files.
            this._logger.error(TelemetryEvent.BinariesUtility_EnsureBinariesPathError, error);
        }

        const downloadLatestAsyncHelper = async (): Promise<[CliClient, KubectlClient]> => {
            this._downloadStarted.trigger();
            await this.cleanUpBeforeDownload(cliExecutablePath, new CommandRunner(this._commandEnvironmentVariables));
            const { cliPath, kubectlPath } = await this.downloadLatestBinariesAsync();
            this._downloadFinished.trigger();
            const cliClient = new CliClient(/*dotNetPath*/null, cliPath, new CommandRunner(this._commandEnvironmentVariables), this._expectedCLIVersion, this._logger);
            const kubectlClient = new KubectlClient(kubectlPath, new CommandRunner(this._commandEnvironmentVariables), this._accountContextManager, this._logger);
            const currentCliVersion = await cliClient.getVersionAsync();
            if (!this.isCliVersionEqual(currentCliVersion)) {
                    const error = new Error(`Current version of the binaries does not match the expected version.`);
                    this._logger.error(TelemetryEvent.UnexpectedError, error, /*properties*/ {
                        expectedVersion: this._expectedCLIVersion,
                        actualVersion: currentCliVersion
                    });
                    throw error;
                }
            return [ cliClient, kubectlClient ];
        };

        if (cliExecutablePath == null || kubectlExecutablePath == null) {
            this._logger.warning(`Binaries are not present locally: downloading them`);
            this._resolveBinariesLocalStatusDeterminedPromise();
            return downloadLatestAsyncHelper();
        }

        let cliClient: CliClient;
        let currentCliVersion: string;
        try {
            cliClient = new CliClient(/*dotNetPath*/null, cliExecutablePath, new CommandRunner(this._commandEnvironmentVariables), this._expectedCLIVersion, this._logger);
            currentCliVersion = await cliClient.getVersionAsync();
        }
        catch (error) {
            this._logger.warning(`Encountered error running CLI: ${error.message}`);
            this._resolveBinariesLocalStatusDeterminedPromise();
            return downloadLatestAsyncHelper();
        }

        if (!this.isCliVersionEqual(currentCliVersion)) {
            this._logger.warning(`CLI binaries are present locally but their version is outdated: downloading the latest ones`, /*error*/ null, /*properties*/ {
                currentCliVersion: currentCliVersion
            });
            this._resolveBinariesLocalStatusDeterminedPromise();
            return downloadLatestAsyncHelper();
        }

        let kubectlClient: KubectlClient;
        try {
            kubectlClient = new KubectlClient(kubectlExecutablePath, new CommandRunner(this._commandEnvironmentVariables), this._accountContextManager, this._logger);

            // Run test to ensure that we can call kubectl
            await kubectlClient.getVersionAsync();
        }
        catch (error) {
            this._logger.warning(`Encountered error running kubectl: ${error.message}`);
            this._resolveBinariesLocalStatusDeterminedPromise();
            return downloadLatestAsyncHelper();
        }

        this._resolveBinariesLocalStatusDeterminedPromise();

        return Promise.resolve([ cliClient, kubectlClient ]);
    }

    // Performs any clean up before the binaries are downloaded.
    // Currently performs `bridge clean-local-connect`
    private async cleanUpBeforeDownload(cliExecutablePath: string, commandRunner: CommandRunner): Promise<void> {
        try {
            if (cliExecutablePath == null) {
                return;
            }
            const oldCliClient = new CliClient(/*dotNetPath*/null, cliExecutablePath, commandRunner, this._expectedCLIVersion, this._logger);
            await oldCliClient.cleanLocalConnectAsync();
            this._logger.trace(TelemetryEvent.BinariesUtility_CleanUpBeforeDownloadSuccess);
        }
        catch (error) {
            this._logger.warning(`Error occured while performing clean up before download. Error: ${error.message}`);
            this._logger.error(TelemetryEvent.BinariesUtility_CleanUpBeforeDownloadError, error);
        }
    }

    /** Return the path to the CLI executable if it exists, undefined if it doesn't */
    private async getCliExecutablePathAsync(): Promise<string | undefined> {
        const binariesDirectoryPath: string = await this.getBinariesDirectoryPathAsync();
        if (binariesDirectoryPath == null) {
            return undefined;
        }
        const cliPath = path.join(binariesDirectoryPath, this.cliExecutableFilePath);
        await fileSystem.accessAsync(cliPath);
        this._logger.trace(`Successfully determined the CLI binary path: ${cliPath}`);
        return cliPath;
    }

    /** Return the path to the kubectl executable if it exists, undefined if it doesn't */
    private async getKubectlExecutablePathAsync(): Promise<string | undefined> {
        const binariesDirectoryPath: string = await this.getBinariesDirectoryPathAsync();
        if (binariesDirectoryPath == null) {
            return undefined;
        }
        const kubectlPath = path.join(binariesDirectoryPath, this.kubectlExecutableFilePath);
        await fileSystem.accessAsync(kubectlPath);
        this._logger.trace(`Successfully determined the kubectl binary path: ${kubectlPath}`);
        return kubectlPath;
    }

    /** Return the path to the binaries directory if it exists, undefined if it doesn't */
    private async getBinariesDirectoryPathAsync(): Promise<string | undefined> {
        const fileDownloader: FileDownloader = await getFileDownloaderApi();
        const unzipDirectory: vscode.Uri = await fileDownloader.tryGetItem(Constants.CliDownloadDirectoryName, this._context);
        if (unzipDirectory == null) {
            return undefined;
        }
        return unzipDirectory.fsPath;
    }

    /**
     * Downloads the newest version of the CLI executable and returns the path to the executable.
     * Checks the CLI version to make sure it is what is expected.
     */
    private async downloadLatestBinariesAsync(): Promise<{ cliPath: string; kubectlPath: string }> {
        const fileDownloaderExtension = this.getFileDownloaderExtension();
        if (!VersionUtility.isVersionSufficient(fileDownloaderExtension.packageJSON[`version`], Constants.FileDownloaderMinVersion)) {
            const error = new Error(Constants.FileDownloaderVersionError);
            this._logger.error(TelemetryEvent.BinariesUtility_FileDownloaderVersionError, error);
            throw error;
        }

        const downloadInfo: ICliDownloadInfo = await this._cliVersionsClient.getDownloadInfoAsync(this._expectedCLIVersion);

        // Only trigger the progress change event when progress increments a whole percent.
        let previousProgress = 0;
        let progress = 0;
        const downloadProgressCallback = (downloadedBytes: number, totalBytes: number): void => {
            const newProgress = Math.floor(downloadedBytes / totalBytes * 100);
            if (newProgress > previousProgress) {
                previousProgress = progress;
                progress = newProgress;
                this._downloadProgress.trigger(progress);
            }
        };

        const downloadStartTime = new Date();
        let downloadSucceeded = false;
        // Download and make sure we can access the executable
        const fileDownloader: FileDownloader = await getFileDownloaderApi();
        let unzipDirectory: vscode.Uri;
        try {
            this._logger.trace(TelemetryEvent.BinariesUtility_DownloadStart);
            unzipDirectory = await fileDownloader.downloadFile(
                vscode.Uri.parse(downloadInfo.downloadUrl),
                Constants.CliDownloadDirectoryName,
                this._context,
                /*cancellationToken*/ undefined,
                downloadProgressCallback,
                {
                    shouldUnzip: true,
                    retries: 8
                }
            );
            this._logger.trace(TelemetryEvent.BinariesUtility_DownloadSuccess);
            downloadSucceeded = true;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BinariesUtility_DownloadError, error);
            throw error;
        }
        finally {
            this._logger.trace(TelemetryEvent.BinariesUtility_OverallDownloadStatus, /*properties*/ {
                binariesDownloadTimeInMilliseconds: new Date().getTime() - downloadStartTime.getTime(),
                binariesDownloadSucceeded: downloadSucceeded
            });
        }

        if (process.platform === `darwin` || process.platform === `linux`) {
            const commandRunner = new CommandRunner(this._commandEnvironmentVariables);
            const chmodPath = process.platform === `darwin` ? `/bin/chmod` : `chmod`;
            await commandRunner.runAsync(chmodPath, [ `+x`, this.cliExecutableFilePath, this.kubectlExecutableFilePath, path.join(`EndpointManager`, `EndpointManager`) ], unzipDirectory.fsPath);
        }

        const cliPath = path.join(unzipDirectory.fsPath, this.cliExecutableFilePath);
        const kubectlPath = path.join(unzipDirectory.fsPath, this.kubectlExecutableFilePath);
        await fileSystem.accessAsync(cliPath);
        await fileSystem.accessAsync(kubectlPath);
        return { cliPath, kubectlPath };
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

    /** Returns true if the current CLI version is equal to the required CLI version */
    private isCliVersionEqual(currentVersion: string): boolean {
        try {
            this._logger.trace(`Found local CLI version: '${currentVersion}'. Expected version: '${this._expectedCLIVersion}'`);
            const isCliVersionSufficient = this._expectedCLIVersion == null || VersionUtility.isVersionSufficient(currentVersion, this._expectedCLIVersion, /*allowLocalBuildFormat*/ true, /*strict*/ true);
            if (isCliVersionSufficient) {
                this._logger.trace(`Local CLI has version number '${currentVersion}', which is equal to the required '${this._expectedCLIVersion}'`);
            }
            else {
                this._logger.trace(`Local CLI has version number '${currentVersion}', which is not equal to the required '${this._expectedCLIVersion}'`);
            }

            return isCliVersionSufficient;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.UnexpectedError, new Error(`Failed to retrieve or parse the CLI version: ${error.message}`));
            throw error;
        }
    }

    private get cliExecutableFilePath(): string {
        const binariesName = this._expectedCLIVersion != null &&
                            (this._expectedCLIVersion <= `1.0.20210708.15` ||
                            this._expectedCLIVersion > `1.0.20210818.0`) ? `dsc` : `bridge`;

        switch (process.platform) {
            case `win32`:
                return binariesName + `.exe`;
            case `darwin`:
            case `linux`:
                return binariesName;
            default:
                const error = new Error(`Unsupported platform: ${process.platform}`);
                this._logger.error(TelemetryEvent.UnexpectedError, error);
                throw error;
        }
    }

    private get kubectlExecutableFilePath(): string {
        switch (process.platform) {
            case `win32`:
                return `kubectl/win/kubectl.exe`;
            case `darwin`:
                return `kubectl/osx/kubectl`;
            case `linux`:
                return `kubectl/linux/kubectl`;
            default:
                const error = new Error(`Unsupported platform: ${process.platform}`);
                this._logger.error(TelemetryEvent.UnexpectedError, error);
                throw error;
        }
    }
}