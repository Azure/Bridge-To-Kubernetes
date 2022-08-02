// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { IDownloadInfo } from "../../models/IBinariesDownloadInfo";
import { ClientType } from "../ClientType";
import { IClient } from "../IClient";

export interface IClientProvider {

    /**
     * Defines the type of client
     */
    readonly Type: ClientType;

    /**
     * Returns the executable file path considering the platform
     */
    getExecutableFilePath(): string;

    /**
     * Gets the download directory of the client
     */
    getDownloadDirectoryName(): string;

    /**
     * Creates an instance of the client
     * @param executablePath The executable file path for the client
     * @param dotNetPath If required, the dotnet runtime required to run the client
     */
    getClient(executablePath: string, dotNetPath: string): IClient;

    /**
     * Returns the min expected version required for the client
     */
    getExpectedVersion(): string;

    /**
     * Returns the information to download the client
     */
    getDownloadInfoAsync(): Promise<IDownloadInfo>;

    /**
     * Returns the executable path considering by client specific environment variables
     */
    getLocalBuildExecutablePath(): string;

    /**
     * Returns list of strings of executable for this client that require
     * updating permissions
     */
    getExecutablesToUpdatePermissions(): string[];
}