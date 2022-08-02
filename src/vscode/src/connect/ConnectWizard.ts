// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// ----------------------------------------------------------------------------
'use strict';

import * as usernameGetter from 'username';
import * as vscode from 'vscode';

import { IBinariesUtility } from '../binaries/IBinariesUtility';
import { IKubeconfigEnrichedContext } from '../clients/KubectlClient';
import { Constants } from '../Constants';
import { DebugAssetsInitializer } from '../debug/DebugAssetsInitializer';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { IKubernetesService } from '../models/IKubernetesService';
import { CheckExtensionSupport } from '../utility/CheckExtensionSupport';
import { KubeconfigCredentialsManager } from '../utility/KubeconfigCredentialsManager';
import { IActionQuickPickItem, IQuickPickParameters, MultiStepInput } from '../utility/MultiStepInput';
import { StringUtility } from '../utility/StringUtility';
import { UrlUtility } from '../utility/UrlUtility';
import { IWizardOutput } from './IWizardOutput';
import { ResourceType } from './ResourceType';

export class ConnectWizard {
    private NumberOfSteps = 4; // Update this number if you add/remove any steps in the wizard
    private readonly _result: Partial<IWizardOutput> = {};
    private _isCreatingNewLaunchConfiguration = false;
    private _isWizardComplete = false;

    public constructor(
        private readonly _binariesUtility: IBinariesUtility,
        private readonly _workspaceFolder: vscode.WorkspaceFolder,
        private readonly _logger: Logger) {
    }

    public async runAsync(
        wizardReason: string,
        targetResourceName: string = null,
        targetResourceNamespace: string = null,
        targetResourceType: ResourceType = ResourceType.Service
    ): Promise<IWizardOutput> {
        const prerequisitesAlertCallback = CheckExtensionSupport.validatePrerequisites(this._logger, /*validatePostDownloadPrerequisites*/ false);
        if (prerequisitesAlertCallback != null) {
            prerequisitesAlertCallback();
            return null;
        }

        this._logger.trace(TelemetryEvent.Connect_WizardStart, {
            wizardReason: wizardReason,
            type: targetResourceType
        });

        if (targetResourceType.toLowerCase() === `pod`) {
            // We skip the isolation step when debugging pods
            this.NumberOfSteps = 3;
        }

        try {
            await MultiStepInput.runAsync(async (input) => {
                // Start displaying the placeholder quickpick as soon as possible, so that the users
                // see immediately that their actions were taken into account.
                // TODO: Refactor the placeholder mechanism so that it works in a more generic way.
                let placeholderMessage = `Choose a ${targetResourceType} to redirect to your machine`;
                if (targetResourceName != null) {
                    placeholderMessage = `Redirecting ${targetResourceType} '${targetResourceName}' to your machine...`;
                }

                input.showPlaceHolderQuickPick({
                    title: this.getInputTitle(),
                    step: 1,
                    totalSteps: this.NumberOfSteps,
                    placeholder: placeholderMessage,
                    items: []
                });

                const kubectlClient = await this._binariesUtility.tryGetKubectlAsync();
                const bridgeClient = await this._binariesUtility.tryGetBridgeAsync();
                let currentContext: IKubeconfigEnrichedContext = null;
                if (kubectlClient != null) {
                    currentContext = await kubectlClient.getCurrentContextAsync();
                }

                if (kubectlClient == null || bridgeClient == null || currentContext == null) {
                    return null;
                }

                if (!await KubeconfigCredentialsManager.refreshCredentialsAsync(currentContext.kubeconfigPath, currentContext.namespace, bridgeClient, this._logger)) {
                    return null;
                }

                if (targetResourceName == null) {
                    // Note: This will only happen when the Configuration flow is activated through the Command Palette. The Command Palette currently only allows
                    // you to select services to debug.
                    if (targetResourceType !== ResourceType.Service) {
                        throw new Error(`Target resource name cannot be unset for resource type ${targetResourceType}`);
                    }
                    return this.pickServiceAsync(input, currentContext, targetResourceType);
                }

                // A target resource was passed in, so we can can skip some configuration steps.
                // Validate the current context against the selected resource
                if (targetResourceNamespace != null && currentContext.namespace != null && currentContext.namespace !== targetResourceNamespace) {
                    throw new Error(`The ${targetResourceType} '${targetResourceName}' belongs to the namespace '${targetResourceNamespace}', `
                                    + `but the current context targets namespace '${currentContext.namespace}'. Please update your kubeconfig to target the correct context.`);
                }

                let namespaces: string[] = null;
                try {
                    namespaces = await kubectlClient.getNamespacesAsync(currentContext.kubeconfigPath);
                }
                catch (error) {
                    // We want to recover if for some reason the user isn't able to list namespaces.
                    this._logger.warning(`Failed to list namespaces`, error);
                }
                if (namespaces != null && !namespaces.includes(targetResourceNamespace)) {
                    // In practice, this error should happen rarely, as the K8s cluster explorer only allows interaction with resources from the current cluster
                    throw new Error(`Failed to find the namespace '${targetResourceNamespace}' in cluster '${currentContext.cluster}'`);
                }

                let resourceNameToPersist: string = targetResourceName;
                if (targetResourceType === ResourceType.Pod) {
                    // Because the specific pod name changes, we persist only the first segments of the name, e.g. <deployment name>-<deployment guid>-.
                    const split: string[] = targetResourceName.split(`-`);
                    if (split.length > 1) {
                        // Delete last segment
                        split.splice(-1, 1);
                        resourceNameToPersist = `${split.join(`-`)}-`;
                    }
                }

                this._result.resourceName = resourceNameToPersist;
                this._result.targetCluster = currentContext.cluster;
                this._result.targetNamespace = targetResourceNamespace;
                this._result.resourceType = targetResourceType;

                return (input: MultiStepInput): Promise<(input: MultiStepInput) => Promise<(input: MultiStepInput) => Promise<void>>> => this.inputPortsAsync(input, targetResourceType);
            });
        }
        catch (error) {
            this._logger.error(TelemetryEvent.Connect_WizardError, error, {
                wizardReason: wizardReason,
                type: targetResourceType,
                resourceName: this._result.resourceName,
                ports: this._result.ports != null ? this._result.ports.join(`,`) : undefined,
                launchConfigurationName: this._result.launchConfigurationName,
                isolateAs: this._result.isolateAs,
                targetCluster: this._result.targetCluster,
                targetNamespace: this._result.targetNamespace,
                isCreatingNewLaunchConfiguration: this._isCreatingNewLaunchConfiguration
            });
            vscode.window.showErrorMessage(`Failed to configure ${Constants.ProductName}: ${error.message}`);
            return null;
        }

        this._logger.trace(TelemetryEvent.Connect_WizardStop, {
            wizardReason: wizardReason,
            type: targetResourceType,
            isWizardComplete: this._isWizardComplete,
            isResourceNameSet: (this._result.resourceName != null && this._result.resourceName.length > 0).toString(),
            ports: this._result.ports != null ? this._result.ports.join(`,`) : undefined,
            launchConfigurationName: this._result.launchConfigurationName,
            isIsolateAsSet: (this._result.isolateAs != null && this._result.isolateAs.length > 0).toString(),
            isTargetClusterSet: (this._result.targetCluster != null && this._result.targetCluster.length > 0).toString(),
            isTargetNamespaceSet: (this._result.targetNamespace != null && this._result.targetNamespace.length > 0).toString(),
            isCreatingNewLaunchConfiguration: this._isCreatingNewLaunchConfiguration
        });
        return this._isWizardComplete ? this._result as IWizardOutput : null;
    }

    private async pickServiceAsync(
        input: MultiStepInput,
        currentContext: IKubeconfigEnrichedContext,
        resourceType: ResourceType
    ): Promise<(input: MultiStepInput) => Promise<(input: MultiStepInput) => Promise<(input: MultiStepInput) => Promise<void>>>> {
        const kubectlClient = await this._binariesUtility.tryGetKubectlAsync();
        if (kubectlClient == null) {
            return null;
        }

        // Store the target cluster/namespace so that we can validate the users are using the right context.
        this._result.targetCluster = currentContext.cluster;
        this._result.targetNamespace = currentContext.namespace;
        this._result.resourceType = resourceType;

        let services: IKubernetesService[] = await kubectlClient.getServicesAsync(currentContext.namespace);
        services = services.filter(service => service.name !== `routingmanager-service`);
        this._logger.trace(TelemetryEvent.Connect_ServiceList, { count: services.length.toString() });
        if (services.length === 0) {
            input.hidePlaceHolderQuickPick();
            throw new Error(`Failed to find any services running in the namespace "${currentContext.namespace}" of cluster "${currentContext.cluster}"`);
        }

        const serviceChoices: vscode.QuickPickItem[] = services.map(service => ({ label: service.name }));

        const pick = await input.showQuickPickAsync({
            title: this.getInputTitle(),
            step: 1,
            totalSteps: this.NumberOfSteps,
            placeholder: `Choose a service to redirect to your machine`,
            items: serviceChoices.sort((s1, s2) => s1.label < s2.label ? -1 : 1),
            activeItem: serviceChoices[0]
        });
        this._result.resourceName = pick.label;

        return (input: MultiStepInput): Promise<(input: MultiStepInput) => Promise<(input: MultiStepInput) => Promise<void>>> => this.inputPortsAsync(input, resourceType);
    }

    private async inputPortsAsync(input: MultiStepInput, resourceType: ResourceType): Promise<(input: MultiStepInput) => Promise<(input: MultiStepInput) => Promise<void>>> {
        const value = await input.showInputBox({
            title: this.getInputTitle(),
            step: 2,
            totalSteps: this.NumberOfSteps,
            value: ``,
            prompt: `Enter your local port such as 80, or 0 if traffic redirection is not needed`,
            validate: this.validatePortInputAsync
        });

        // At this point, we know that the input is valid, as it passed the validateInput check.
        this._result.ports = [ Number(value) ];

        return (input: MultiStepInput): Promise<(input: MultiStepInput) => Promise<void>> => this.pickLaunchConfigurationAsync(input, resourceType);
    }

    private async validatePortInputAsync(value: string): Promise<string> {
        if (value == null || value.length === 0) {
            return `A value is required (enter 0 if traffic redirection is not needed)`;
        }

        const portNum = Number(value);
        if (isNaN(portNum) || portNum < 0 || portNum >= 65536) {
            return `Port must be a number between 0 and 65535`;
        }

        // The port input is valid. No message to return.
        return undefined;
    }

    private async pickLaunchConfigurationAsync(input: MultiStepInput, resourceType: ResourceType): Promise<(input: MultiStepInput) => Promise<void>> {
        const launchConfigurations = this.getAvailableDebugConfigurations();
        const createNewLaunchConfigurationLabel = `$(plus) Create a new launch configuration`;
        const noLaunchConfigurationLabel = `$(warning) Configure ${Constants.ProductName} without a launch configuration`;
        const choices: vscode.QuickPickItem[] = [
            ...launchConfigurations.map(config => ({
                label: config[`name`],
                detail: `Choose this launch configuration if it is the one you use to run your code locally`
            })),
            {
                label: createNewLaunchConfigurationLabel,
                detail: `${Constants.ProductName} requires a valid launch configuration to run your code locally`
            },
            {
                label: noLaunchConfigurationLabel,
                detail: `${Constants.ProductName} will connect to your cluster, but you will need to run your code manually`
            }
        ];

        const pick = await input.showQuickPickAsync({
            title: this.getInputTitle(),
            step: 3,
            totalSteps: this.NumberOfSteps,
            placeholder: `Choose the launch configuration to use to run your component locally`,
            items: choices,
            activeItem: choices[0]
        });

        if (pick.label === createNewLaunchConfigurationLabel) {
            vscode.window.showInformationMessage(`Create your new launch configuration, and restart the configuration of  ${Constants.ProductName}.`);
            vscode.commands.executeCommand(`debug.addConfiguration`, this._workspaceFolder.uri);
            this._isCreatingNewLaunchConfiguration = true;
            return undefined;
        }

        this._result.launchConfigurationName = pick.label !== noLaunchConfigurationLabel ? pick.label : undefined;

        const username: string = await usernameGetter();
        const routingHeader: string = await StringUtility.generateRoutingHeaderAsync(username);
        return (input: MultiStepInput): Promise<void> => this.pickIsolationModeAsync(input, routingHeader, resourceType);
    }

    private async pickIsolationModeAsync(input: MultiStepInput, routingHeader: string, resourceType: ResourceType): Promise<void> {
        if (resourceType.toLowerCase() === `pod`) {
            // We do not support isolation mode when debugging a single pod
            this._isWizardComplete = true;
            return;
        }

        const noChoice: vscode.QuickPickItem = {
            label: `No`,
            detail: `Redirect all incoming requests to your machine, including those from other developers.`
        };

        const yesChoice: vscode.QuickPickItem = {
            label: `Yes`,
            detail: `Only redirect requests from the "${routingHeader}" subdomain (requires header propagation).`
        };

        const learnMoreChoice: IActionQuickPickItem = {
            label: `$(link-external) Learn More`,
            action: (): void => {
                UrlUtility.openUrl(`https://aka.ms/bridge-to-k8s-isolation`);
            }
        };

        const choices: vscode.QuickPickItem[] = [ noChoice, yesChoice, learnMoreChoice ];

        const pick = await input.showQuickPickAsync<IActionQuickPickItem, IQuickPickParameters<IActionQuickPickItem>>({
            title: this.getInputTitle(),
            step: 4,
            totalSteps: this.NumberOfSteps,
            placeholder: `Isolate your local version of "${this._result.resourceName}" from other developers?`,
            items: choices,
            activeItem: choices[0]
        });
        this._result.isolateAs = (pick === yesChoice) ? routingHeader : null;

        this._isWizardComplete = true;
    }

    private getInputTitle(): string {
        let title = `Connect to Kubernetes`;

        if (this._result.resourceName != null && this._result.resourceName.length > 0) {
            title += ` service: ${this._result.resourceName}`;
        }
        else {
            return title;
        }

        if (this._result.ports != null && this._result.ports.length > 0) {
            // tslint:disable-next-line quotemark
            title += `:${this._result.ports.join(',')}`;
        }

        return title;
    }

    private getAvailableDebugConfigurations(): object[] {
        const launchConfig: vscode.WorkspaceConfiguration = vscode.workspace.getConfiguration(`launch`, this._workspaceFolder.uri);
        const debugConfigurations: object[] = launchConfig.get<{}[]>(`configurations`, /*defaultValue*/ []);
        return debugConfigurations.filter(debugConfiguration =>
            !DebugAssetsInitializer.isConnectConfiguration(debugConfiguration[`type`])
            && !DebugAssetsInitializer.isTraditionalDevSpacesDebugConfiguration(debugConfiguration[`name`])
            && !DebugAssetsInitializer.isConnectTask(debugConfiguration[`preLaunchTask`]));
    }
}
