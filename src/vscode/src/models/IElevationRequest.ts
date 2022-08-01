// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { IPortInformation } from "./IPortInformation";

export interface IFreePortElevationRequest {
    readonly requestType: `FreePort`;
    targetPortInformation: IPortInformation[];
    targetType: string;
}

export interface IEditHostsFileElevationRequest {
    readonly requestType: `EditHostsFile`;
}

export type IElevationRequest = IFreePortElevationRequest | IEditHostsFileElevationRequest;