// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as vscode from 'vscode';
import * as k8s from 'vscode-kubernetes-tools-api';
import { ResourceType } from '../connect/ResourceType';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { WorkspaceFolderManager } from '../WorkspaceFolderManager';

export class LocalTunnelDebuggingManager {
    public constructor(
        private readonly _workspaceFolderManager: WorkspaceFolderManager,
        private readonly _logger: Logger) {
    }

    public async registerLocalTunnelDebugProviderAsync(): Promise<boolean> {
        const localTunnelDebugger = await k8s.extension.localTunnelDebugger.v1;

        // Can't do `!localTunnelDebugger.available`, since for some reason the compiler complains about accessing the `reason` field below if we do.
        // However, in this case we also need to disable the linter because it doesn't like the unnecessary boolean comparison.
        /* tslint:disable */
        if (localTunnelDebugger.available === false) {
            /* tslint:enable */
            this._logger.warning(`Unable to register provider: ${localTunnelDebugger.reason}`);
            return false;
        }
        const localTunnelDebugProvider = {
            id: Constants.ProductName,
            startLocalTunnelDebugging: (target?: any): Promise<void> => this.startDebugSessionAsync(target)
        };
        localTunnelDebugger.api.registerLocalTunnelDebugProvider(localTunnelDebugProvider);
        return true;
    }

    // Main entrypoint through the k8s.LocalTunnelDebugProvider API
    private async startDebugSessionAsync(target?: any): Promise<void> {
        this._logger.trace(TelemetryEvent.DebugLocalTunnel_SessionStarted);
        const clusterExplorer = await k8s.extension.clusterExplorer.v1;

        // Can't do `!clusterExplorer.available`, since for some reason the compiler complains about accessing the `reason` field below if we do.
        // However, in this case we also need to disable the linter because it doesn't like the unnecessary boolean comparison.
        /* tslint:disable */
        if (clusterExplorer.available === false) {
            /* tslint:enable */
            this._logger.warning(`Unable to resolve command target: ${clusterExplorer.reason}`);
            return;
        }
        const resolvedTarget: k8s.ClusterExplorerV1.ClusterExplorerNode | undefined = clusterExplorer.api.resolveCommandTarget(target);
        if (resolvedTarget === undefined) {

            // The Debug (Local Tunnel) option was selected through the command palette. Go through normal configure & debug flow.
            this._logger.trace(TelemetryEvent.DebugLocalTunnel_CommandPaletteSelected);
            await this.pickFolderAndStartDebuggingAsync();
            return;
        }

        // The Debug (Local Tunnel) option was selected through a resource in the cluster explorer
        if (resolvedTarget.nodeType === `resource`) {
            switch (resolvedTarget.resourceKind.manifestKind) {
                case `Service`:
                    this._logger.trace(TelemetryEvent.DebugLocalTunnel_ServiceSelected);
                    await this.pickFolderAndStartDebuggingAsync(resolvedTarget.name, resolvedTarget.namespace, ResourceType.Service);
                    return;
                case `Job`:
                    this._logger.trace(TelemetryEvent.DebugLocalTunnel_JobSelected);
                    break;
                case `Pod`:
                    this._logger.trace(TelemetryEvent.DebugLocalTunnel_PodSelected);
                    await this.pickFolderAndStartDebuggingAsync(resolvedTarget.name, resolvedTarget.namespace, ResourceType.Pod);
                    return;
                case `Deployment`:
                    this._logger.trace(TelemetryEvent.DebugLocalTunnel_DeploymentSelected);
                    break;
                default:
                    // If we see this event, it means the K8s extension has been extended to support Local Tunnel debugging on more kinds of resources.
                    this._logger.trace(TelemetryEvent.DebugLocalTunnel_UnknownResourceSelected, { resourceKind: resolvedTarget.resourceKind.manifestKind });
            }

            const resourceNotSupportedMessage = `${Constants.ProductName} only supports debugging Kubernetes services and pods for now, and does not yet support debugging objects of type '${resolvedTarget.resourceKind.manifestKind}'.`
                                                + ` To debug a service, open Network > Services in the Kubernetes view and select 'Debug (Local Tunnel)' on your service.`
                                                + ` To make a feature request or stay up to date on feature announcements, please see https://aka.ms/bridge-to-k8s-report.`;
            vscode.window.showInformationMessage(resourceNotSupportedMessage);
            return;
        }

        // If for some reason the K8s extension decides to allow this command on other node types, we'll respond with helpful details.
        // This node type could be something like folder node, a cluster or a helm release.
        // For more info see https://github.com/azure/vscode-kubernetes-tools/blob/master/docs/extending/commandtargets.md
        this._logger.trace(TelemetryEvent.DebugLocalTunnel_UnknownNodeSelected, { nodeType: resolvedTarget.nodeType });
        vscode.window.showInformationMessage(`${Constants.ProductName} does not support debugging on this item.`);
    }

    private async pickFolderAndStartDebuggingAsync(targetResourceName: string = null, targetResourceNamespace: string = null, targetResourceType: ResourceType = ResourceType.Service): Promise<void> {
        const folderToUse: vscode.WorkspaceFolder = await this._workspaceFolderManager.pickCommandWorkspaceFolderAsync(vscode.workspace.workspaceFolders);
        if (folderToUse == null) {
            vscode.window.showInformationMessage(`To debug with ${Constants.ProductName}, at least one source code folder must be open.`);
            return;
        }

        // TODO(ansoedal): validate that the resource type of the debug configuration matches
        let debugConfiguration: string = await this._workspaceFolderManager.retrieveBridgeConfigurationDebugAssetsPresentInFolderAsync(folderToUse, `LocalTunnelDebuggingStarted`);
        if (debugConfiguration == null) {
            // No configuration present. Direct user to the configuration wizard
            debugConfiguration = await this._workspaceFolderManager.configureFolderAsync(
                folderToUse,
                targetResourceName,
                targetResourceNamespace,
                targetResourceType
            );
            if (debugConfiguration == null) {
                // User may have cancelled the configuration wizard
                return;
            }
        }

        vscode.debug.startDebugging(folderToUse, debugConfiguration);
    }
}