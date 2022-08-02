// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { ClientType } from '../clients/ClientType';

export interface IClient {

    /**
     * Defines the type of client
     */
    readonly Type: ClientType;

    /**
     * Returns the version the client
     */
    getVersionAsync(): Promise<string>;

    /**
     * Returns the path of the executable
     */
    getExecutablePath(): string;
}