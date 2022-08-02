// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as path from 'path';
import * as vscode from 'vscode';
import { IExperimentationService } from 'vscode-tas-client';
import { ResourceType } from '../connect/ResourceType';

import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { IAuthenticationTarget } from '../models/IAuthenticationTarget';
import { IElevationRequest } from '../models/IElevationRequest';
import { EnvironmentUtility } from '../utility/EnvironmentUtility';
import { IReadOnlyEventSource, IReleasable } from '../utility/Event';
import { RetryUtility } from '../utility/RetryUtility';
import { ClientType } from './ClientType';
import { CommandRunner } from './CommandRunner';
import { IClient } from './IClient';

// Surfaces available Bridge commands.
// NOTE: because there is only a single instance of CommandRunner in this class, with the same outputEmitted event,
// a different instance of this class should be use for different commands
export class BridgeClient implements IClient {
    private readonly _dotNetDirectory: string;

    public constructor(
        dotNetPath: string,
        private readonly _executablePath: string,
        private readonly _commandRunner: CommandRunner,
        private readonly _expectedBridgeVersion: string,
        private readonly _logger: Logger
    ) {
        this._dotNetDirectory = dotNetPath != null ? path.dirname(dotNetPath) : null;
     }

    public readonly Type: ClientType = ClientType.Bridge;

    public get outputEmitted(): IReadOnlyEventSource<string> {
        return this._commandRunner.outputEmitted;
    }

    public async getVersionAsync(): Promise<string> {
        try {
            // Adding retries to ensure the bridge client is intialized properly
            // as this command is executed right after the download.
            const getVersionAsyncFn = async (): Promise<string> => {
                const args: string[] = [ `--version` ];
                const output: string = await this._commandRunner.runAsync(
                    this._executablePath,
                    args,
                    null /* currentWorkingDirectory */,
                    this.getRequiredEnvironmentVariables());
                // The output of bridge --version will be:
                //     0.1.20181023.7
                // for prod, dev, staging and "0.1.0.11071149-username" or "0.1.0.11071149" for local builds.
                const bridgeVersion: string = output.trim();

                this._logger.trace(TelemetryEvent.BridgeClient_GetVersionSuccess);
                return bridgeVersion;
            };
            return await RetryUtility.retryAsync<string>(getVersionAsyncFn, /*retries*/3, /*delayInMs*/100);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BridgeClient_GetVersionError, error);
            throw error;
        }
    }

    public async checkCredentialsAsync(kubeconfigPath: string, namespace: string): Promise<void> {
        const args: string[] = [ `check-credentials`, `--namespace`, namespace ];

        const customEnvironmentVariables: NodeJS.ProcessEnv = { ...this.getRequiredEnvironmentVariables(), ...this.getCustomEnvironmentVariables(kubeconfigPath) };

        try {
            await this._commandRunner.runAsync(
                this._executablePath,
                args,
                null /*currentWorkingDirectory*/,
                customEnvironmentVariables
            );
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BridgeClient_CheckCredentialsError, error);
            throw error;
        }
        this._logger.trace(TelemetryEvent.BridgeClient_CheckCredentialsSuccess);
    }

    public async refreshCredentialsAsync(kubeconfigPath: string, namespace: string, authenticationTargetCallback: (target: IAuthenticationTarget) => any): Promise<void> {
        const args: string[] = [ `refresh-credentials`, `--namespace`, namespace ];

        const customEnvironmentVariables: NodeJS.ProcessEnv = { ...this.getRequiredEnvironmentVariables(), ...this.getCustomEnvironmentVariables(kubeconfigPath) };

        const onOutputEmittedReleasable: IReleasable = this._commandRunner.outputEmitted.subscribe((data: string) => {
            try {
                    const authTarget: IAuthenticationTarget = JSON.parse(data.toString());
                    if (authTarget.authenticationCode != null && authTarget.url != null) {
                        // This happens if this callback is called on data that does not come from the refresh-credentials commands.
                        // As of today the BridgeClient (and other clients as well) are singleton retuned by the IBinaryUtility and used everywhere.
                        // These singlentons share the same CommandRunner underneath with the same outpuitEmitted event.
                        // Even releasing the registration below doesn't solve the problem, so unless we refactor the clients the only solution is to ignore output that is not meant for this callback
                        authenticationTargetCallback(authTarget);
                    }
            }
            catch (error) {
                this._logger.error(TelemetryEvent.BridgeClient_RefreshCredentialsOutputError, error);
                // StdOutput should only contain the authTarget, if something bad happens it should be output by Bridge on stderr
            }
        });

        try {
            await this._commandRunner.runAsync(
                this._executablePath,
                args,
                null /*currentWorkingDirectory*/,
                customEnvironmentVariables
            );
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BridgeClient_RefreshCredentialsError, error);
            throw error;
        }
        finally {
            onOutputEmittedReleasable.release();
        }

        this._logger.trace(TelemetryEvent.BridgeClient_RefreshCredentialsSuccess);
    }

    public async prepConnectAsync(
        currentWorkingDirectory: string,
        kubeconfigPath: string,
        resourceType: string,
        resource: string,
        currentNamespace: string): Promise<IElevationRequest[]> {
        try {
            let resourceTypeFlag: string;
            switch (resourceType) {
                case ResourceType.Service:
                    resourceTypeFlag = `--service`;
                    break;
                case ResourceType.Pod:
                    resourceTypeFlag = `--pod`;
                    break;
                default:
                    throw new Error(`Unable to determine the resource flag for resource '${resourceType}'`);
            }
            let args: string[];
            if (this._expectedBridgeVersion == null || this._expectedBridgeVersion > `1.0.20210615.1`) { // Note: Expected version is null if vsix was built locally
                args = [ `prep-connect`, `--output`, `json`, resourceTypeFlag, resource, `--namespace`, currentNamespace ];
            }
            else {
                args = [ `prep-connect`, `--output`, `json`, `--namespace`, currentNamespace ];
            }

            const customEnvironmentVariables: NodeJS.ProcessEnv = { ...this.getRequiredEnvironmentVariables(), ...this.getCustomEnvironmentVariables(kubeconfigPath) };
            const output: string = await this._commandRunner.runAsync(
                this._executablePath,
                args,
                currentWorkingDirectory,
                customEnvironmentVariables
            );

            this._logger.trace(`Elevation requests output: ${output}`);
            // If other output besides JSON has been returned from prep-connect, we must first trim it off.
            // Bug 1359701: We should fix this in the CLI rather than pushing the problem on the extension.
            const trimmedOutput = output.substring(output.indexOf(`[`), output.lastIndexOf(`]`) + 1);
            const requests = JSON.parse(trimmedOutput.replace(/[\r\n]/g, ``));

            this._logger.trace(TelemetryEvent.BridgeClient_PrepConnectSuccess);

            return requests;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BridgeClient_PrepConnectError, error);
            throw new Error(`Failed to validate the requirements to replicate resources locally: ${error.message}`);
        }
    }

    public async connectAsync(
        currentWorkingDirectory: string,
        kubeconfigPath: string,
        resource: string,
        resourceType: ResourceType,
        ports: number[],
        controlPort: number,
        envFilePath: string,
        scriptFilePath: string,
        parentProcessId: string,
        elevationRequests: IElevationRequest[],
        isolateAs: string,
        currentNamespace: string,
        useKubernetesServiceEnvironmentVariables: boolean,
        experimentationService: IExperimentationService): Promise<void> {
        try {
            let resourceTypeFlag: string;
            switch (resourceType) {
                case ResourceType.Service:
                    resourceTypeFlag = `--service`;
                    break;
                case ResourceType.Pod:
                    resourceTypeFlag = `--pod`;
                    break;
                default:
                    throw new Error(`Unable to determine the resource flag for resource '${resourceType}'`);
            }

            const args: string[] = [
                `connect`,
                resourceTypeFlag, resource,
                `--env`, envFilePath,
                `--script`, scriptFilePath,
                `--control-port`, controlPort.toString(),
                `--ppid`, parentProcessId,
                `--namespace`, currentNamespace
            ];

            if (elevationRequests != null) {
                args.push(`--elevation-requests`, JSON.stringify(elevationRequests));
            }

            if (isolateAs != null) {
                args.push(`--routing`, isolateAs);
            }

            if (useKubernetesServiceEnvironmentVariables) {
                args.push(`--use-kubernetes-service-environment-variables`);
            }

            const routingManagerFeatureFlags = this.getRoutingManagerFeatureFlags(experimentationService);
            if (routingManagerFeatureFlags != null) {
                routingManagerFeatureFlags.forEach((featureFlag: string) => {
                    args.push(`--routing-manager-feature-flag`, featureFlag);
                });
            }

            ports.forEach((port: number) => {
                if (port > 0) {
                    args.push(`--local-port`, port.toString());
                }
            });

            const customEnvironmentVariables: NodeJS.ProcessEnv = { ...this.getRequiredEnvironmentVariables(), ...this.getCustomEnvironmentVariables(kubeconfigPath) };
            customEnvironmentVariables[`ALLOW_NON_AZURE_CLUSTERS`] = true.toString();
            await this._commandRunner.runAsync(
                this._executablePath,
                args,
                currentWorkingDirectory,
                customEnvironmentVariables
            );

            this._logger.trace(TelemetryEvent.BridgeClient_ConnectSuccess);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.BridgeClient_ConnectError, error);
            throw error;
        }
    }

    public async cleanLocalConnectAsync(): Promise<void> {
        try {
            const args: string[] = [ `clean-local-connect` ];
            await this._commandRunner.runAsync(
                this._executablePath,
                args,
                null /* currentWorkingDirectory */,
                this.getRequiredEnvironmentVariables());
            this._logger.trace(TelemetryEvent.BridgeClient_CleanLocalConnectSuccess);
        }
        catch (error) {
            if (error.message.includes(`Failed to load kubeconfig`)) {
                // This error is noisy and unactionable for us, so we log it as a warning
                this._logger.warning(`Encountered kubeconfig issue: ${error.message}`);
            }
            else {
                this._logger.error(TelemetryEvent.BridgeClient_CleanLocalConnectError, error);
                throw error;
            }
        }
    }

    public getExecutablePath(): string {
        return this._executablePath;
    }

    private getRequiredEnvironmentVariables(): NodeJS.ProcessEnv {
        const shouldCollectTelemetry: boolean = vscode.workspace.getConfiguration().get<boolean>(`telemetry.enableTelemetry`);
        const requiredEnvironmentVariables: NodeJS.ProcessEnv = {
            BRIDGE_COLLECT_TELEMETRY: shouldCollectTelemetry.toString(),
            BRIDGE_CORRELATION_ID: vscode.env.sessionId,
            DOTNET_SYSTEM_GLOBALIZATION_INVARIANT: `true`,
            BRIDGE_IS_CODESPACES: (EnvironmentUtility.isCodespacesEnvironment()).toString()
        };

        if (this._dotNetDirectory != null) {
            Object.assign(requiredEnvironmentVariables, {
                DOTNET_ROOT: this._dotNetDirectory
            });
        }

        return requiredEnvironmentVariables;
    }

    private getCustomEnvironmentVariables(kubeConfigPath: string): NodeJS.ProcessEnv {
        return {
            KUBECONFIG: kubeConfigPath
        };
    }

    private getRoutingManagerFeatureFlags(experimentationService: IExperimentationService): string[] {
        const routingManagerFeatureFlags: string[] = [];
        // Todo Use the below block of code to enable a feature flag for routing manager
        // const featureFlagEnabled = experimentationService.getTreatmentVariable<bool>(/*configId*/ `vscode`, /*name*/ `featureName`);
        // this._logger.trace(`Call to ExP for routing manager feature flag <featureFlagName> returned '${featureFlagEnabled}'`);
        // if (featureFlagEnabled) {
        //     routingManagerFeatureFlags.push(`featureName`);
        // }
        return routingManagerFeatureFlags;
    }
}