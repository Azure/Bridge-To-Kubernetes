// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

export interface IKubernetesService {
    name: string;
    namespace: string;
    selector: Map<string, string>;
}