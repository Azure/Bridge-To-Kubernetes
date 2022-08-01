// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';
import { IExperimentationService } from 'vscode-tas-client';

import { IBinariesUtility } from '../binaries/IBinariesUtility';
import { IKubeconfigEnrichedContext, KubectlClient } from '../clients/KubectlClient';
import { ConnectionStatus, ConnectWorkspaceFolder, IConnectionTarget } from '../connect/ConnectWorkspaceFolder';
import { ResourceType } from '../connect/ResourceType';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { IReleasable } from '../utility/Event';
import { UrlUtility } from '../utility/UrlUtility';
import { TaskTerminalBase } from './TaskTerminalBase';

export class ConnectServiceTaskTerminal extends TaskTerminalBase {
    private readonly UseKubernetesServiceEnvironmentVariablesDisabled: string = `useKubernetesServiceEnvironmentVariablesDisabledV2`;

    public constructor(
        private readonly _context: vscode.ExtensionContext,
        private readonly _connectWorkspaceFolder: ConnectWorkspaceFolder,
        private readonly _resourceName: string,
        private readonly _resourceType: ResourceType,
        private readonly _ports: number[],
        private readonly _isolateAs: string,
        private readonly _targetCluster: string,
        private readonly _targetNamespace: string,
        private readonly _useKubernetesServiceEnvironmentVariables: boolean,
        private readonly _experimentationService: IExperimentationService,
        private readonly _binariesUtility: IBinariesUtility,
        private readonly _logger: Logger,
        private readonly _connectStartedCallback: (alreadyConnected: boolean) => void) {
        super();
    }

    public async open(initialDimensions: vscode.TerminalDimensions | undefined): Promise<void> {
        if (this._connectWorkspaceFolder.connectionStatus === ConnectionStatus.Connected) {
            const connectionTarget: IConnectionTarget = this._connectWorkspaceFolder.connectionTarget;
            // Make sure that the current connection corresponds to the parameters we provided.
            if (connectionTarget.resourceName === this._resourceName
                && connectionTarget.ports.length === this._ports.length
                && connectionTarget.ports.every((value: number, index: number) => value === this._ports[index])
                && connectionTarget.isolateAs === this._isolateAs
                && connectionTarget.targetCluster === this._targetCluster
                && connectionTarget.targetNamespace === this._targetNamespace) {
                this._writeLine(`Kubernetes ${this._resourceType} ${this._resourceName} is redirected successfully to your machine.`);
                this._logger.trace(`A Connect terminal was started despite the Connect connection status being already 'Connected'. Ignoring the new call.`);
                this._connectStartedCallback(/*alreadyConnected*/ true);
                this._exit(0);
                return;
            }
            else {
                // Stop the current Connect session.
                this._logger.warning(`A Connect terminal was started for a connection different from the one currently connected.`);
                await this._connectWorkspaceFolder.stopConnectAsync();
            }
        }

        const onOutputEmittedReleasable: IReleasable = this._connectWorkspaceFolder.connectionOutputEmitted.subscribe((data: string) => {
            this._write(`${data}`);
        });

        const kubectlClient: KubectlClient = await this._binariesUtility.tryGetKubectlAsync();
        if (kubectlClient == null) {
            this._writeLine(`This action requires dependencies that are not available yet. Please look at the "Kubernetes" item in the status bar for more information.`);
            this._exit(1);
            return;
        }

        try {
            this._logger.trace(TelemetryEvent.ConnectServiceTaskTerminal_StartConnect, {
                resourceName: this._resourceName,
                ports: this._ports.join(`, `),
                isolateAs: this._isolateAs
            });

            this._writeLine(`Redirecting Kubernetes ${this._resourceType} ${this._resourceName} to your machine...`);

            const currentContext: IKubeconfigEnrichedContext = await kubectlClient.getCurrentContextAsync();
            if (this._targetCluster != null) {
                this._writeLine(`Target cluster: ${this._targetCluster}`);
            }
            this._writeLine(`Current cluster: ${currentContext.cluster}`);
            if (this._targetNamespace != null) {
                this._writeLine(`Target namespace: ${this._targetNamespace}`);
            }
            this._writeLine(`Current namespace: ${currentContext.namespace}`);
            this._writeLine(`Target ${this._resourceType} name: ${this._resourceName}`);
            this._writeLine(`Target ${this._resourceType} ports: ${this._ports.join(`, `)}`);
            if (this._isolateAs != null) {
                this._writeLine(`Isolating ${this._resourceType} with routing header: ${this._isolateAs}`);
            }
            this._writeLine(`Using kubernetes service environment variables: ${this._useKubernetesServiceEnvironmentVariables}`);
            this._writeLine(``);

            const success: boolean = await this._connectWorkspaceFolder.startConnectAsync(
                this._resourceName,
                this._resourceType,
                this._ports,
                this._isolateAs,
                this._targetCluster,
                this._targetNamespace,
                this._useKubernetesServiceEnvironmentVariables,
                this._experimentationService);
            if (!success) {
                this._writeLine(``);
                this._exit(1);
                return;
            }

            this._connectStartedCallback(/*alreadyConnected*/ false);
            this._exit(0);
        }
        catch (error) {
            this._writeLine(``);
            this._writeLine(`Error: ${error.message}`);
            vscode.window.showErrorMessage(error.message);
            this.showUseKubernetesServiceEnvironmentVariablesPromptIfRequired(error.message);
            this._logger.error(TelemetryEvent.ConnectServiceTaskTerminal_Error, error);
            this._exit(1);
        }
        finally {
            // Make sure we stop outputting whatever the Connect result was.
            onOutputEmittedReleasable.release();
        }
    }

    private showUseKubernetesServiceEnvironmentVariablesPromptIfRequired(connectFailureMessage: string): void {
        if (connectFailureMessage == null) {
            return;
        }

        const isUseKubernetesServiceEnvironmentVariablesDisabled: boolean = this._context.globalState.get<boolean>(this.UseKubernetesServiceEnvironmentVariablesDisabled, /*defaultValue*/ false);
        if (isUseKubernetesServiceEnvironmentVariablesDisabled) {
            return;
        }

        const showUseKubernetesServiceEnvironmentVariablesPrompt: boolean = Constants.ListOfErrorMessagesForUsingKubernetesServiceEnvironmentVariables.some((errorMessage) => {
            return connectFailureMessage.toLowerCase().indexOf(errorMessage.toLowerCase()) !== -1;
        });

        if (showUseKubernetesServiceEnvironmentVariablesPrompt) {
            this._logger.trace(TelemetryEvent.UseKubernetesServiceEnvironmentVariables_PromptShown);
            const learnMore = `Learn More`;
            const neverShowAgain = `Never Show Again`;
            vscode.window.showInformationMessage(`Consider trying Kubernetes service environment variables to avoid seeing errors with ${Constants.EndpointManagerName} or freeing ports.`, learnMore, neverShowAgain).then((selectedValue: string) => {
                if (selectedValue === learnMore) {
                    this._logger.trace(TelemetryEvent.UseKubernetesServiceEnvironmentVariablesPrompt_LearnMoreClicked);
                    UrlUtility.openUrl(`https://aka.ms/use-k8s-svc-env-vars`);
                }
                else if (selectedValue === neverShowAgain) {
                    this._logger.trace(TelemetryEvent.UseKubernetesServiceEnvironmentVariables_PromptDisabled);
                    this._context.globalState.update(this.UseKubernetesServiceEnvironmentVariablesDisabled, true);
                }
            });
        }
    }
}