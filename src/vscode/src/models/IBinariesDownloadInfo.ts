// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { ClientType } from '../clients/ClientType';

export interface IDownloadInfo {
    downloadUrl: string;
    sha256Hash: string;
}

export interface IBinariesDownloadInfo {
    downloadInfoMap: Map<ClientType, IDownloadInfo>;
}
