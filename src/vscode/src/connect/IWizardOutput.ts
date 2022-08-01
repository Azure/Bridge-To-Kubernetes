// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import { ResourceType } from "./ResourceType";

export interface IWizardOutput {
    resourceName: string;
    resourceType: ResourceType;
    ports: number[];
    launchConfigurationName: string;
    isolateAs: string;
    targetCluster: string;
    targetNamespace: string;
}