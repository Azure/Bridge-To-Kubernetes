// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';
import * as vscode from 'vscode';
import { IExperimentationService } from 'vscode-tas-client';
import { Logger } from '../logger/Logger';
import { Environment, EnvironmentUtility } from './EnvironmentUtility';

export class VersionUtility {
    // Returns true if 'currentVersion' is greater than or equal to 'minVersion', otherwise false
    // When allowLocalBuildFormat is set to true, skips the validation when third part of the version is '0'. By default this value is false
    // When strict is set to true, only returns true if the version is strictly equal.
    public static isVersionSufficient(currentVersion: string, minVersion: string, allowLocalBuildFormat: boolean = false, strict: boolean = false): boolean {
            const versionArray: string[] = currentVersion.split(`.`);

            // Handle local builds where third part of the version is 0.
            if ((versionArray[2] === `0` || versionArray[2].indexOf(`-`) > -1) && allowLocalBuildFormat) {
                return true;
            }

            const minVersionArray: string[] = minVersion.split(`.`);
            if (versionArray.length !== minVersionArray.length) {
                throw new Error(`Invalid version format: ${currentVersion}`);
            }

            for (let i = 0; i < versionArray.length; ++i) {
                const isIntegerPositive: boolean = /^\d+$/.test(versionArray[i]);
                if (!isIntegerPositive) {
                    throw new Error(`Invalid version: ${currentVersion}`);
                }

                const versionPart: number = Number(versionArray[i]);
                const minVersionPart: number = Number(minVersionArray[i]);

                if (!strict && versionPart > minVersionPart) {
                    return true;
                }

                if (versionPart === minVersionPart) {
                    continue;
                }

                return false;
            }

            return true;
    }

    // Returns the CLI version that this VSCode session expects
    public static async getExpectedCliVersionAsync(context: vscode.ExtensionContext, experimentationService: IExperimentationService, packageJsonContent: object, logger: Logger): Promise<string> {
        const bridgeEnvironment = EnvironmentUtility.getBridgeEnvironment(logger);

        // This call will return the upcoming version for treatment group; control & everyone else will get the stable prod version. Undefined if the flight is currently inactive.
        // TODO(ansoedal): Disable ExP permanently. For now this call will return undefined, as long as the treatment variable doesn't exist
        const expBinariesVersion = experimentationService.getTreatmentVariable<string>(/*configId*/ `vscode`, /*name*/ `mindaroBinariesVersion-doesnotexist`);
        logger.trace(`Call to ExP returned version '${expBinariesVersion}'`);

        // How we resolve the expected CLI version:
        //  1. Is the BRIDGE_CLI_VERSION environment variable set? Use that one.
        //  2. Is a flight in progress? Get the binaries version from ExP.
        //  3. Fall back onto whichever LKS version was specified in package.json. (For dev and staging, this is whichever version matches this extension. For Prod, this is a variable we specify in the Mindaro-Connect-VSCode pipeline.)
        const packageJsonVersion: string = packageJsonContent[`extensionMetadata`][`expectedCLIVersion`];
        const expectedCLIVersion = process.env.BRIDGE_CLI_VERSION != null ? process.env.BRIDGE_CLI_VERSION :
            (expBinariesVersion != null && bridgeEnvironment === Environment.Production ? expBinariesVersion :
            (process.env.BRIDGE_BUILD_PATH != null ? null :
            (packageJsonVersion != null && !packageJsonVersion.startsWith(`$`) ? packageJsonVersion : null)));

        logger.trace(`Resolved expected CLI version '${expectedCLIVersion}'`);
        if (expectedCLIVersion != null && !/^\d+(\.\d+){3}$/.test(expectedCLIVersion)) {
            throw new Error(`Invalid minimum CLI version '${expectedCLIVersion}' found in package.json file.`);
        }

        return expectedCLIVersion;
    }
}