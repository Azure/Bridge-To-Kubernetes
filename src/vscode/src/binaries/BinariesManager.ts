// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';
import * as vscode from 'vscode';

import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { AccountContextManager } from '../models/context/AccountContextManager';
import { BinariesUtility } from './BinariesUtility';
import { BinariesUtilityV2 } from './BinariesUtilityV2';
import { IBinariesUtility } from './IBinariesUtility';

export class BinariesManager {
    public static getBinariesUtility(logger: Logger,
                                     context: vscode.ExtensionContext,
                                     commandEnvironmentVariables: NodeJS.ProcessEnv,
                                     accountContextManager: AccountContextManager,
                                     expectedCLIVersion: string
    ): IBinariesUtility {
        if (this.useBinariesUtilityV2()) {
            logger.trace(TelemetryEvent.BinariesUtility_VersionV2);
            return new BinariesUtilityV2(logger, context, commandEnvironmentVariables, accountContextManager, expectedCLIVersion);
        }

        logger.trace(TelemetryEvent.BinariesUtility_VersionV1);
        return new BinariesUtility(logger, context, commandEnvironmentVariables, accountContextManager, expectedCLIVersion);
    }

    private static useBinariesUtilityV2(): boolean {
        const useBinaryUtilityEnvironmentVariable: string = process.env.BRIDGE_BINARYUTILITYVERSION;
        if (useBinaryUtilityEnvironmentVariable != null) {
            if (useBinaryUtilityEnvironmentVariable.toLowerCase() === `v1`) {
                return false;
            }
            else if (useBinaryUtilityEnvironmentVariable.toLowerCase() === `v2`) {
                return true;
            }
        }
        const userMachineID: string = vscode.env.machineId;
        if (userMachineID == null || userMachineID === `someValue.machineId`) {
            return true;
        }

        for (const firstCharacter of Constants.FirstCharacterOfMachineIDToUseBinariesUtilityV2) {
            if (userMachineID.startsWith(firstCharacter)) {
                return true;
            }
        }

        return false;
    }
}