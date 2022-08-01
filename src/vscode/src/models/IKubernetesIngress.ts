// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export interface IKubernetesIngress {
    name: string;
    namespace: string;
    host: string;
    protocol: string;
    clonedFromName: string;
}