// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';

import { IBinariesUtility } from './binaries/IBinariesUtility';
import { BridgeClient } from './clients/BridgeClient';
import { IKubeconfigEnrichedContext, KubectlClient } from './clients/KubectlClient';
import { Constants } from './Constants';
import { Logger } from './logger/Logger';
import { TelemetryEvent } from './logger/TelemetryEvent';
import { AccountContextManager } from './models/context/AccountContextManager';
import { IKubernetesIngress } from './models/IKubernetesIngress';
import { IPromptItem, PromptResult } from './PromptItem';
import { CheckExtensionSupport } from './utility/CheckExtensionSupport';
import { Environment, EnvironmentUtility } from './utility/EnvironmentUtility';
import { EventSource, IReadOnlyEventSource } from './utility/Event';
import { KubeconfigCredentialsManager as KubeconfigCredentialsManager } from './utility/KubeconfigCredentialsManager';
import { ThenableUtility } from './utility/ThenableUtility';
import { UrlUtility } from './utility/UrlUtility';

export interface IStatusBarMenuItem extends vscode.QuickPickItem {
    callback: () => void;
}

export enum StatusBarItemGroup {
    Context = 0,
    PreConnect = 1,
    Connect = 2,
    Ingresses = 3,
    ExternalLinks = 4
}

export class StatusBarMenu {
    public static readonly OpenMenuCommand = `mindaro.open-menu`;
    private readonly _itemsByGroup: Map<StatusBarItemGroup, IStatusBarMenuItem[]>;
    private readonly _statusBarItem: vscode.StatusBarItem;
    private readonly _initializationPromise: Promise<void>;
    private readonly _refreshStatusBarMenuItemsTriggered: EventSource<void>;
    private _resolveInitializationPromise: () => void;
    private _logger: Logger;
    private _accountContextManager: AccountContextManager;
    private _binariesUtility: IBinariesUtility;
    private _isLoading: boolean;
    private _isUpdatingDependencies: boolean;
    private _prerequisitesAlertCallback: () => void;
    private _isMenuOpened: boolean;
    private _currentKubeconfigContextStatusBarMenuItem: IStatusBarMenuItem;
    private _extensionIdentifier = `Kubernetes`;
    private _statusBarText = this._extensionIdentifier;
    private _currentKubeconfigContext: IKubeconfigEnrichedContext;
    private _shouldCredentialsCheckBeSilent = true;
    private _hasSubscribedToKubeconfigChanges = false;

    public constructor(
        context: vscode.ExtensionContext
    ) {
        this._itemsByGroup = new Map<StatusBarItemGroup, IStatusBarMenuItem[]>();
        this._initializationPromise = new Promise<void>((resolve): void => {
            this._resolveInitializationPromise = resolve;
        });
        this._refreshStatusBarMenuItemsTriggered = new EventSource<void>();
        this._isLoading = true;
        this._isUpdatingDependencies = false;
        this._prerequisitesAlertCallback = null;
        this._isMenuOpened = false;

        context.subscriptions.push(vscode.commands.registerCommand(StatusBarMenu.OpenMenuCommand, async () => {
            await this._initializationPromise;

            if (this._prerequisitesAlertCallback != null) {
                this._prerequisitesAlertCallback();
                return;
            }

            await this.openMenuAsync();
        }));

        this._statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left);
        this.refreshStatusItem();
        this._statusBarItem.show();
    }

    public refreshStatusBarMenuItemsTriggered(): IReadOnlyEventSource<void> {
        return this._refreshStatusBarMenuItemsTriggered;
    }

    public async initializeAsync(
        logger: Logger,
        binariesUtility: IBinariesUtility,
        accountContextManager: AccountContextManager): Promise<void> {
        this._logger = logger;
        this._accountContextManager = accountContextManager;
        this._binariesUtility = binariesUtility;

        const environment: Environment = EnvironmentUtility.getBridgeEnvironment(this._logger);
        if (environment !== Environment.Production) {
            // Show in the status bar the BRIDGE_ENVIRONMENT if we're not in production.
            this._extensionIdentifier = `[${environment.toUpperCase()}] ${this._extensionIdentifier}`;
        }

        await this.validateAndInitializeMenuItemsAsync();

        vscode.workspace.onDidChangeConfiguration((configurationChangeEvent: vscode.ConfigurationChangeEvent) => {
            if (configurationChangeEvent.affectsConfiguration(`tasks`)) {
                // Clean and refresh the PreConnect status bar menu items.
                this._itemsByGroup.set(StatusBarItemGroup.PreConnect, []);
                this._refreshStatusBarMenuItemsTriggered.trigger();
            }
        });
    }

    public addItem(group: StatusBarItemGroup, label: string, description: string, callback: () => void): IStatusBarMenuItem {
        const statusBarMenuItem: IStatusBarMenuItem = {
            label: label,
            description: description,
            callback: callback
        };
        this._itemsByGroup.get(group).push(statusBarMenuItem);
        this.refreshStatusItem();
        return statusBarMenuItem;
    }

    public removeItem(group: StatusBarItemGroup, item: IStatusBarMenuItem): void {
        let items: IStatusBarMenuItem[] = this._itemsByGroup.get(group);
        items = items.filter(x => x !== item);
        this._itemsByGroup.set(group, items);
        this.refreshStatusItem();
    }

    public async triggerIngressesRefreshAsync(isolateAs: string = null): Promise<void> {
        this._isLoading = true;
        this.refreshStatusItem();
        if (this._currentKubeconfigContext != null) {
            const kubectlClient = await this._binariesUtility.tryGetKubectlAsync();
            await this.refreshIngressesAsync(kubectlClient, this._currentKubeconfigContext, isolateAs);
        }

        this._isLoading = false;
        this.refreshStatusItem();
    }

    private resetStatusBarMenuItems(): void {
        this._itemsByGroup.clear();
        this._itemsByGroup.set(StatusBarItemGroup.Context, []);
        this._itemsByGroup.set(StatusBarItemGroup.PreConnect, []);
        this._itemsByGroup.set(StatusBarItemGroup.Connect, []);
        this._itemsByGroup.set(StatusBarItemGroup.Ingresses, []);
        this._itemsByGroup.set(StatusBarItemGroup.ExternalLinks, []);
    }

    private async validateAndInitializeMenuItemsAsync(): Promise<boolean> {
        this._isLoading = true;
        this.refreshStatusItem();

        const arePrerequisitesValid: boolean = await this.validatePrerequisitesAsync();
        if (arePrerequisitesValid) {
            this.resetStatusBarMenuItems();
            this._currentKubeconfigContextStatusBarMenuItem = this.addItem(StatusBarItemGroup.Context, /*label*/ null, /*description*/ null, async () => {
                await this.redirectToKubernetesViewAsync();
            });

            this.addItem(StatusBarItemGroup.ExternalLinks, `$(info) About ${Constants.ProductName}`, /*description*/ null, () => {
                UrlUtility.openUrl(`https://aka.ms/bridge-to-k8s-vscode-quickstart`);
            });

            this.addItem(StatusBarItemGroup.ExternalLinks, `$(bug) Report an issue`, /*description*/ null, () => {
                UrlUtility.openUrl(`https://aka.ms/mindaro-issues`);
            });

            this._refreshStatusBarMenuItemsTriggered.trigger();

            await this.refreshCurrentKubeconfigContextAsync();
            // We only subscribe to kubeconfig changes once, even though we might trigger the initialization loop multiple times.
            if (!this._hasSubscribedToKubeconfigChanges) {
                this._accountContextManager.getKubeconfigChanged().subscribe(() => {
                    // If the StatusBarMenu is already refreshing its context, ignore the change to not create parallel refreshes.
                    if (this._isLoading) {
                        return;
                    }

                    // We're about to start again a whole initialization loop with the new changed kubeconfig, refreshing everything
                    // from scratch. Thus, the _currentKubeconfigContext must be cleared as if we were starting a new session.
                    this._currentKubeconfigContext = null;

                    this._shouldCredentialsCheckBeSilent = true;
                    this.validateAndInitializeMenuItemsAsync();
                });
                this._hasSubscribedToKubeconfigChanges = true;
            }
        }

        this._isLoading = false;
        this.refreshStatusItem();
        this._resolveInitializationPromise();
        return arePrerequisitesValid;
    }

    private async validatePrerequisitesAsync(): Promise<boolean> {
        try {
            const preDownloadPrequisitesAlertCallback  = CheckExtensionSupport.preDownloadValidatePrerequisites(this._logger);
            if (preDownloadPrequisitesAlertCallback  != null) {
                this._prerequisitesAlertCallback = preDownloadPrequisitesAlertCallback;
                return false;
            }

            // Register the event handlers in case we need to update binaries.
            this._binariesUtility.downloadStarted().subscribe(() => {
                this._isUpdatingDependencies = true;
                this._statusBarText = `${this._extensionIdentifier}: Updating dependencies (0%)`;
                this.refreshStatusItem();
            });

            this._binariesUtility.downloadProgress().subscribe((percentComplete: number) => {
                // Making sure we switch to "updating dependencies" mode even if the
                // status bar menu is not the class that started the binaries update.
                this._isUpdatingDependencies = true;
                this._statusBarText = `${this._extensionIdentifier}: Updating dependencies (${percentComplete}%)`;
                this.refreshStatusItem();
            });

            this._binariesUtility.downloadFinished().subscribe(() => {
                this._isUpdatingDependencies = false;
                this._statusBarText = `${this._extensionIdentifier}`;
                this.refreshStatusItem();
            });

            let kubectlClient: KubectlClient;
            let bridgeClient: BridgeClient;
            try {
                const binaries: [BridgeClient, KubectlClient] = await this._binariesUtility.ensureBinariesAsync();
                bridgeClient = binaries[0];
                kubectlClient = binaries[1];
            }
            catch (error) {
                this._isUpdatingDependencies = false;
                this._statusBarText = `${this._extensionIdentifier}`;
                if (error.message === Constants.FileDownloaderVersionError) {
                    const downloadFileDownloader = `Update extension manually`;
                    this._prerequisitesAlertCallback = async (): Promise<void> => {
                        const selectedValue: string = await ThenableUtility.ToPromise(vscode.window.showErrorMessage(
                            `Failed to update dependencies: ${error.message}`, downloadFileDownloader));
                        if (selectedValue === downloadFileDownloader) {
                            // Provide a link to manually update the File Downloader extension
                            vscode.commands.executeCommand(`extension.open`, `mindaro-dev.file-downloader`);
                        }
                    };
                }
                else {
                    const retryDependencies = `Retry Dependencies Update`;
                    const installManually = `Install Manually`;
                    this._prerequisitesAlertCallback = async (): Promise<void> => {
                        const selectedValue: string = await ThenableUtility.ToPromise(vscode.window.showErrorMessage(
                            `Failed to update dependencies: ${error.message}`, retryDependencies, installManually));
                        if (selectedValue === retryDependencies) {
                            // Retrying to run the prerequisites check, including dependencies update.
                            this.validateAndInitializeMenuItemsAsync();
                        }
                        else if (selectedValue === installManually) {
                            UrlUtility.openUrl(`https://github.com/microsoft/mindaro/issues/32`);
                        }
                    };
                }
                this._logger.warning(`Failed to update dependencies`, error);
                return false;
            }

            let credentialsCheckCurrentKubeconfigContext: IKubeconfigEnrichedContext = null;
            try {
                // Determine if we have a valid current context to read from in the kubeconfig.
                credentialsCheckCurrentKubeconfigContext = await kubectlClient.getCurrentContextAsync();
            }
            catch (error) {
                this._prerequisitesAlertCallback = (): void => {
                    // Try to run once more the initialization logic, and display an error if not working.
                    this.validateAndInitializeMenuItemsAsync().then((arePrerequisitesValid: boolean) => {
                        if (!arePrerequisitesValid) {
                            this.promptToSetCurrentKubeconfigContext(error);
                        }
                    });
                };
                this._logger.warning(`Failed to retrieve a valid context from the kubeconfig present locally`, error);
                return false;
            }

            const postDownloadPrequisitesAlertCallback = CheckExtensionSupport.postDownloadValidatePrerequisites(this._logger);
            if (postDownloadPrequisitesAlertCallback != null) {
                this._prerequisitesAlertCallback = async (): Promise<void> => {
                    postDownloadPrequisitesAlertCallback();
                    await this.validateAndInitializeMenuItemsAsync();
                };
                return false;
            }

            // Determine if the current context needs the token to be refreshed
            let areCredentialsValid: boolean;
            if (this._shouldCredentialsCheckBeSilent) {
                areCredentialsValid = await KubeconfigCredentialsManager.checkCredentialsAsync(credentialsCheckCurrentKubeconfigContext.kubeconfigPath, credentialsCheckCurrentKubeconfigContext.namespace, bridgeClient, this._logger);
            }
            else {
                areCredentialsValid = await KubeconfigCredentialsManager.refreshCredentialsAsync(credentialsCheckCurrentKubeconfigContext.kubeconfigPath, credentialsCheckCurrentKubeconfigContext.namespace, bridgeClient, this._logger);
            }
            if (!areCredentialsValid) {
                this._shouldCredentialsCheckBeSilent = false;
                this._prerequisitesAlertCallback = (): void => {
                    this.validateAndInitializeMenuItemsAsync();
                };
                this._logger.warning(`Kubeconfig credentials need to be refreshed`);
                return false;
            }

            this._prerequisitesAlertCallback = null;
        }
        catch (error) {
            this._prerequisitesAlertCallback = (): void => {
                vscode.window.showErrorMessage(`Failed to validate the prerequisites required to use ${Constants.ProductName}: ${error.message}`);
            };
            this._logger.error(TelemetryEvent.StatusBar_ValidatePrerequisitesError, error);
            return false;
        }

        return true;
    }

    private async openMenuAsync(): Promise<void> {
        if (!await this.refreshCurrentKubeconfigContextAsync(/*promptUser*/ true)) {
            return;
        }

        // Create on purpose a new quick pick every time we open the menu,
        // to bypass a bug where quickpick.show() doesn't always work.
        const quickPick: vscode.QuickPick<IStatusBarMenuItem> = vscode.window.createQuickPick();
        // Configures the quick pick.
        quickPick.onDidChangeSelection(async (items: IStatusBarMenuItem[]) => {
            quickPick.hide();
            if (items.length === 0) {
                this._logger.error(TelemetryEvent.UnexpectedError, new Error(`Status bar menu selection was triggered with ${items.length} items`));
                return;
            }
            items[0].callback();
        });
        quickPick.onDidHide(() => {
            this._isMenuOpened = false;
            this.refreshStatusItem();
            quickPick.dispose();
        });

        // Populate the quick pick items in the right order.
        quickPick.items = [
            ...this._itemsByGroup.get(StatusBarItemGroup.Context),
            ...this._itemsByGroup.get(StatusBarItemGroup.PreConnect),
            ...this._itemsByGroup.get(StatusBarItemGroup.Connect),
            ...this._itemsByGroup.get(StatusBarItemGroup.Ingresses),
            ...this._itemsByGroup.get(StatusBarItemGroup.ExternalLinks)
        ];
        quickPick.show();

        this._isMenuOpened = true;
        this.refreshStatusItem();
        this._logger.trace(TelemetryEvent.StatusBar_MenuOpened);
    }

    private async refreshCurrentKubeconfigContextAsync(promptUser: boolean = false): Promise<boolean> {
        try {
            this._isLoading = true;
            this.refreshStatusItem();
            const kubectlClient = await this._binariesUtility.tryGetKubectlAsync();
            if (kubectlClient == null) {
                return false;
            }

            const otherKubeconfigContext: IKubeconfigEnrichedContext = await kubectlClient.getCurrentContextAsync();
            if (!this.areEqualContext(this._currentKubeconfigContext, otherKubeconfigContext)) {
                this._currentKubeconfigContext = otherKubeconfigContext;
                await this.refreshIngressesAsync(kubectlClient, this._currentKubeconfigContext);
                this._currentKubeconfigContextStatusBarMenuItem.label = `Current namespace: ${this._currentKubeconfigContext.namespace}`;
                this._currentKubeconfigContextStatusBarMenuItem.description = `${this._currentKubeconfigContext.cluster} (${this._currentKubeconfigContext.kubeconfigPath})`;
                this._statusBarText = `${this._extensionIdentifier}: ${this._currentKubeconfigContext.namespace} (${this._currentKubeconfigContext.cluster})`;
            }

            this._isLoading = false;
            this.refreshStatusItem();
            return true;
        }
        catch (error) {
            if (promptUser) {
                this.promptToSetCurrentKubeconfigContext(error);
            }
            this._isLoading = false;
            this._currentKubeconfigContext = null;
            this.refreshStatusItem();
            return false;
        }
    }

    private promptToSetCurrentKubeconfigContext(error: Error): void {
        const yesItem: IPromptItem = { title: `Set current cluster and namespace`, result: PromptResult.Yes };
        vscode.window.showErrorMessage(`${error.message}`, yesItem).then(async (resultItem: IPromptItem) => {
            if (resultItem === yesItem) {
                await this.redirectToKubernetesViewAsync();
            }
        });
    }

    private async refreshIngressesAsync(kubectlClient: KubectlClient, currentKubeconfigContext: IKubeconfigEnrichedContext, isolateAs: string = null): Promise<void> {
        try {
            this._itemsByGroup.set(StatusBarItemGroup.Ingresses, []);
            let ingressesMap: Map<string, IKubernetesIngress>;
            let loadBalancerIngresses: IKubernetesIngress[];
            [ ingressesMap, loadBalancerIngresses ] = await Promise.all([
                kubectlClient.getIngressesAsync(currentKubeconfigContext.namespace, currentKubeconfigContext.kubeconfigPath, /*quiet*/ true).then(
                    (ingressesArray: IKubernetesIngress[]) => new Map(ingressesArray.map(i => [ i.name, i ]))),
                kubectlClient.getLoadBalancerIngressesAsync(currentKubeconfigContext.namespace, currentKubeconfigContext.kubeconfigPath, /*quiet*/ true)
            ]);

            if (isolateAs != null) {
                for (const ingress of ingressesMap.values()) {
                    const fullUrl = `${ingress.protocol}://${ingress.host}`;
                    // Only show default ingresses and current user's cloned ingresses.
                    if (ingress.host.startsWith(`${isolateAs}.`) && ingress.clonedFromName != null) {
                        const ingressIdentifier = `${ingress.clonedFromName} isolated on '${isolateAs}'`;
                        this.addItem(StatusBarItemGroup.Ingresses, `$(globe) Go to ${ingressIdentifier}`, fullUrl, () => {
                            UrlUtility.openUrl(fullUrl);
                        });
                    }
                    else if (ingress.clonedFromName == null) {
                        this.addItem(StatusBarItemGroup.Ingresses, `$(globe) Go to ${ingress.name}`, fullUrl, () => {
                            UrlUtility.openUrl(fullUrl);
                        });
                    }
                }
            }
            else {
                for (const ingress of ingressesMap.values()) {
                    // In case of non-isolated mode, do not show cloned ingresses
                    if (ingress.clonedFromName == null) {
                        const fullUrl = `${ingress.protocol}://${ingress.host}`;
                        this.addItem(StatusBarItemGroup.Ingresses, `$(globe) Go to ${ingress.name}`, fullUrl, () => {
                            UrlUtility.openUrl(fullUrl);
                        });
                    }
                }
            }

            for (const loadBalancerIngress of loadBalancerIngresses) {
                const fullUrl = `${loadBalancerIngress.protocol}://${loadBalancerIngress.host}`;
                if (ingressesMap.has(loadBalancerIngress.name)) {
                    this._logger.trace(`A load balancer already exists for "${loadBalancerIngress.name}". Skipping the load balancer Ingress.`);
                    continue;
                }

                this.addItem(StatusBarItemGroup.Ingresses, `$(globe) Go to ${loadBalancerIngress.name}`, fullUrl, () => {
                    UrlUtility.openUrl(fullUrl);
                });

                if (isolateAs != null) {
                    const isolatedUrl = `${loadBalancerIngress.protocol}://${isolateAs}.${loadBalancerIngress.host}.nip.io`;
                    this.addItem(StatusBarItemGroup.Ingresses, `$(globe) Go to ${loadBalancerIngress.name} isolated on '${isolateAs}'`, isolatedUrl, () => {
                        UrlUtility.openUrl(isolatedUrl);
                    });
                }
            }
        }
        catch (error) {
            this._logger.warning(`Failed to retrieve the available ingresses, skipping adding them to the menu. Error: ${error.message}`);
        }
    }

    private refreshStatusItem(): void {
        let icon: string;
        if (this._isUpdatingDependencies && this._prerequisitesAlertCallback == null) {
            icon = `$(cloud-download)`;
        }
        else if (this._isLoading) {
            icon = `$(loading~spin)`;
        }
        else if (this._prerequisitesAlertCallback != null || this._currentKubeconfigContext == null) {
            icon = `$(alert)`;
        }
        else if (this._itemsByGroup.get(StatusBarItemGroup.Connect).length > 0) {
            icon = `$(broadcast)`;
        }
        else if (this._isMenuOpened) {
            icon = `$(fold-down)`;
        }
        else {
            icon = `$(fold-up)`;
        }

        this._statusBarItem.text = `${icon} ${this._statusBarText}`;
        // Prevents the button from being clickable if we're loading/updating dependencies.
        this._statusBarItem.command = (this._isLoading || this._isUpdatingDependencies) ? null : StatusBarMenu.OpenMenuCommand;
    }

    private async redirectToKubernetesViewAsync(): Promise<void> {
        vscode.window.showInformationMessage(`Change the current cluster, namespace and kubeconfig from the Clusters section of the Kubernetes view.`);
        await vscode.commands.executeCommand(`workbench.view.extension.kubernetesView`);
    }

    private areEqualContext(currentKubeconfigContext: IKubeconfigEnrichedContext, otherContext: IKubeconfigEnrichedContext): boolean {
        if (currentKubeconfigContext == null && otherContext == null) {
            return true;
        }

        if (currentKubeconfigContext == null ||
            otherContext == null ||
            currentKubeconfigContext.cluster !== otherContext.cluster ||
            currentKubeconfigContext.fqdn !== otherContext.fqdn ||
            currentKubeconfigContext.namespace !== otherContext.namespace ||
            currentKubeconfigContext.kubeconfigPath !== otherContext.kubeconfigPath) {
            return false;
        }

        return true;
    }
}