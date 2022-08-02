// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { BridgeClient } from "../clients/BridgeClient";
import { KubectlClient } from "../clients/KubectlClient";
import { IReadOnlyEventSource } from "../utility/Event";

export interface IBinariesUtility {

    downloadStarted(): IReadOnlyEventSource<void>;

    downloadFinished(): IReadOnlyEventSource<void>;

    downloadProgress(): IReadOnlyEventSource<number>;

    ensureBinariesAsync(): Promise<[BridgeClient, KubectlClient]>;

    tryGetBridgeAsync(): Promise<BridgeClient>;

    tryGetKubectlAsync(): Promise<KubectlClient>;
}