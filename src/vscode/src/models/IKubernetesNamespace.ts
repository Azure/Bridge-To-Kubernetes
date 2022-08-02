// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export interface IKubernetesNamespace {
    readonly isDevSpace: false;
    name: string;
}