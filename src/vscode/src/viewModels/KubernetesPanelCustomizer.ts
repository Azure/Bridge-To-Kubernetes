// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';
import * as k8s from 'vscode-kubernetes-tools-api';

import { IBinariesUtility } from '../binaries/IBinariesUtility';
import { IKubeconfigEnrichedContext, KubectlClient } from '../clients/KubectlClient';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { CheckExtensionSupport } from '../utility/CheckExtensionSupport';
import { EventSource, IReadOnlyEventSource } from '../utility/Event';

export class KubernetesPanelCustomizer implements k8s.ClusterExplorerV1.NodeUICustomizer {
    private _currentAksCluster: string;
    private _currentAksClusterChanged: EventSource<string>;

    public constructor(
        private readonly _binariesUtility: IBinariesUtility,
        private readonly _logger: Logger) {
        this._currentAksCluster = null;
        this._currentAksClusterChanged = new EventSource<string>();
    }

    public async initializeAsync(): Promise<void> {
        // Fire and forget: we just want to trigger the evaluation of the current kubeconfig
        // context so that subscribers of currentAksClusterChanged can get it early.
        this.evaluateCurrentContextAsync();

        const clusterExplorer: k8s.API<k8s.ClusterExplorerV1> = await k8s.extension.clusterExplorer.v1;
        if (!clusterExplorer.available) {
            this._logger.error(TelemetryEvent.KubernetesPanelCustomizer_ApiUnavailableError, new Error(`ClusterExplorer API is not available`));
            return;
        }
        clusterExplorer.api.registerNodeUICustomizer(this);
    }

    public get currentAksClusterChanged(): IReadOnlyEventSource<string> {
        return this._currentAksClusterChanged;
    }

    public customize(node: k8s.ClusterExplorerV1.ClusterExplorerNode, treeItem: vscode.TreeItem): void | Thenable<void> {
        // Here a Kubernetes context is used as a synonym for a Kubernetes cluster.
        if (node.nodeType === `context`) {
            return this.customizeCurrentContextNode(node, treeItem);
        }

        if (node.nodeType === `context.inactive` && this._currentAksCluster != null && this._currentAksCluster === treeItem.label) {
            // The cluster previously stored as current is now inactive.
            this._currentAksCluster = null;
            this._currentAksClusterChanged.trigger(this._currentAksCluster);
        }
    }

    private customizeCurrentContextNode(
        node: k8s.ClusterExplorerV1.ClusterExplorerContextNode | k8s.ClusterExplorerV1.ClusterExplorerInactiveContextNode,
        treeItem: vscode.TreeItem
    ): void {
        try {
            // Fire and forget: we don't want to prevent the Kubernetes extension from initializing because of us.
            this.evaluateCurrentContextAsync();
        }
        catch (error) {
            this._logger.warning(`Failed to customize the current context node`, error);
        }
    }

    private async evaluateCurrentContextAsync(): Promise<void> {
        const prerequisitesAlertCallback = CheckExtensionSupport.validatePrerequisites(this._logger);
        if (prerequisitesAlertCallback != null) {
            return;
        }

        let kubectlClient: KubectlClient;
        try {
            kubectlClient = (await this._binariesUtility.ensureBinariesAsync())[1];
        }
        catch (error) {
            // We couldn't retrieve the required binaries. Ignoring as this is a fire and forget method.
            return;
        }

        const currentContext: IKubeconfigEnrichedContext = await kubectlClient.getCurrentContextAsync();
        const currentFqdnDomain: string = this.getFqdnDomain(currentContext.fqdn);
        const fqdns: string[] = await kubectlClient.getAllFqdnsAsync();
        const fqdnDomains: string[] = [];
        for (const fqdn of fqdns) {
            fqdnDomains.push(this.getFqdnDomain(fqdn));
        }

        this._logger.trace(TelemetryEvent.KubernetesPanelCustomizer_SupportedFqdnEvaluation, {
            currentFqdnDomain: currentFqdnDomain,
            clustersCount: fqdnDomains.length,
            fqdnDomains: fqdnDomains.join(`,`)
        });
    }

    private getFqdnDomain(fqdn: string): string {
        // We can get a null FQDN if the server string in kubeconfig isn't a valid URL.
        if (fqdn != null) {
            const fqdnParts: string[] = fqdn.split(`.`);
            if (fqdnParts.length > 2) {
                return `${fqdnParts[fqdnParts.length - 2]}.${fqdnParts[fqdnParts.length - 1]}`;
            }
        }

        return `unknown`;
    }
}