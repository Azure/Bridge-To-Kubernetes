// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------

import * as path from 'path';
import * as vscode from 'vscode';
import { getExperimentationServiceAsync, IExperimentationService, IExperimentationTelemetry, TargetPopulation } from 'vscode-tas-client';
import { Logger } from './logger/Logger';

/* __GDPR__
    "query-expfeature" : {
        "ABExp.queriedFeature": { "classification": "SystemMetaData", "purpose": "FeatureInsight" }
    }
*/

interface IProductConfiguration {
    quality?: `stable` | `insider` | `exploration`;
}

async function getProductConfig(appRoot: string): Promise<IProductConfiguration> {
    const raw = await vscode.workspace.fs.readFile(vscode.Uri.file(path.join(appRoot, `product.json`)));
    return JSON.parse(raw.toString());
}

interface IPackageConfiguration {
    name: string;
    publisher: string;
    version: string;
}

async function getPackageConfig(packageFolder: string): Promise<IPackageConfiguration> {
    const raw = await vscode.workspace.fs.readFile(vscode.Uri.file(path.join(packageFolder, `package.json`)));
    return JSON.parse(raw.toString());
}

export class ExperimentationTelemetry implements IExperimentationTelemetry {

    constructor(private baseLogger: Logger) { }

    public setSharedProperty(name: string, value: string): void {
        this.baseLogger.setSharedProperty(name, value);
    }

    public postEvent(eventName: string, props: Map<string, string>): void {
        const event: Record<string, string> = {};
        for (const [ key, value ] of props) {
            event[key] = value;
        }
        this.baseLogger.trace(eventName, event);
    }
}

function getTargetPopulation(product: IProductConfiguration): TargetPopulation {
    switch (product.quality) {
        case `stable`: return TargetPopulation.Public;
        case `insider`: return TargetPopulation.Insiders;
        case `exploration`: return TargetPopulation.Internal;
        case undefined: return TargetPopulation.Team;
        default: return TargetPopulation.Public;
    }
}

class NullExperimentationService implements IExperimentationService {
    public readonly initializePromise: Promise<void> = Promise.resolve();
    public readonly initialFetch: Promise<void> = Promise.resolve();

    public isFlightEnabled(flight: string): boolean {
        return false;
    }

    public async isCachedFlightEnabled(flight: string): Promise<boolean> {
        return Promise.resolve(false);
    }

    public async isFlightEnabledAsync(flight: string): Promise<boolean> {
        return Promise.resolve(false);
    }

    public getTreatmentVariable<T extends boolean | number | string>(configId: string, name: string): T | undefined {
        return undefined;
    }

    public async getTreatmentVariableAsync<T extends boolean | number | string>(configId: string, name: string): Promise<T | undefined> {
        return Promise.resolve(undefined);
    }
}

export async function createExperimentationServiceAsync(context: vscode.ExtensionContext, experimentationTelemetry: ExperimentationTelemetry): Promise<IExperimentationService> {
    const pkg = await getPackageConfig(context.extensionPath);
    const product = await getProductConfig(vscode.env.appRoot);
    const targetPopulation = getTargetPopulation(product);

    // We only create a real experimentation service for the stable version of the extension, not insiders.
    return pkg.name === `mindaro`
        ? getExperimentationServiceAsync(`${pkg.publisher}.${pkg.name}`, pkg.version, targetPopulation, experimentationTelemetry, context.globalState)
        : new NullExperimentationService();
}