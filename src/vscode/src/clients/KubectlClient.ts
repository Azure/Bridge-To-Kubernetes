// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as dns from 'dns';

import { ClientType } from '../clients/ClientType';
import { Constants } from '../Constants';
import { Logger } from '../logger/Logger';
import { TelemetryEvent } from '../logger/TelemetryEvent';
import { AccountContextManager } from '../models/context/AccountContextManager';
import { IKubernetesIngress } from '../models/IKubernetesIngress';
import { IKubernetesService } from '../models/IKubernetesService';
import { RetryUtility } from '../utility/RetryUtility';
import { CommandRunner } from './CommandRunner';
import { IClient } from './IClient';

export interface IKubeconfigEnrichedContext {
    cluster: string;
    namespace: string;
    fqdn: string;
    kubeconfigPath: string;
}

export class KubectlClient implements IClient {
    private readonly ClonedFromIngressMetadataLabel = `routing.visualstudio.io/generated`;
    private readonly ClonedFromAnnotationName: string = `routing.visualstudio.io/clonedFrom`;
    public constructor(
        private readonly _executablePath: string,
        private readonly _commandRunner: CommandRunner,
        private readonly _accountContextManager: AccountContextManager,
        private readonly _logger: Logger) {
    }

    public readonly Type: ClientType = ClientType.Kubectl;

    public async getCurrentContextAsync(): Promise<IKubeconfigEnrichedContext> {
        let kubeconfigPath: string = null;
        let kubectlOutput: string = null;
        try {
            kubeconfigPath = await this._accountContextManager.getKubeconfigPathAsync(/*shouldDisplayErrorIfNeeded*/ false);
            kubectlOutput = await this.runKubectlCommandAsync([ `config`, `view`, `--minify`, `-o`, `json` ], kubeconfigPath, /*quiet*/ true);
        }
        catch (error) {
            if (error.message.includes(`error: current-context must exist in order to minify`) && kubeconfigPath != null) {
                // Special case: the kubeconfig file doesn't exist. Let's improve the error message to help the user.
                error = new Error(`No kubeconfig file found at path ${kubeconfigPath}`);
            }
            this._logger.error(TelemetryEvent.KubectlClient_CurrentContextRetrievalError, error);
            throw new Error(`Failed to get the current context from the kubeconfig: ${error.message}`);
        }

        let config: object = null;
        try {
            config = JSON.parse(kubectlOutput);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.KubectlClient_CurrentContextRetrievalError, error);
            throw new Error(`Failed to parse the current context from the kubeconfig: ${error.message}. Data: ${kubectlOutput}`);
        }

        try {
            const currentContext: object = config[`contexts`][0][`context`];
            const currentCluster: object = config[`clusters`][0][`cluster`];
            const serverUrl = new URL(currentCluster[`server`]);
            const currentNamespace = currentContext[`namespace`];
            return {
                cluster: currentContext[`cluster`],
                namespace: currentNamespace != null ? currentNamespace : `default`,
                fqdn: serverUrl.hostname,
                kubeconfigPath: kubeconfigPath
            };
        }
        catch (error) {
            this._logger.error(TelemetryEvent.KubectlClient_CurrentContextRetrievalError, error);
            throw new Error(`Failed to get the current context from the kubeconfig: ${error.message}`);
        }
    }

    public async getAllFqdnsAsync(): Promise<string[]> {
        try {
            const kubeconfigPath: string = await this._accountContextManager.getKubeconfigPathAsync(/*shouldDisplayErrorIfNeeded*/ false);
            const kubectlOutput: string = await this.runKubectlCommandAsync([ `config`, `view`, `-o`, `jsonpath={.clusters[*].cluster.server}` ], kubeconfigPath);
            const servers: string[] = kubectlOutput.split(` `);
            const fqdns: string[] = servers.map((server: string) => {
                try {
                    const serverUrl = new URL(server);
                    return serverUrl.hostname;
                }
                catch (error) {
                    this._logger.warning(`Impossible to parse the server '${server}' as a URL`, error);
                    return null;
                }
            });
            return fqdns;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.KubectlClient_AllFqdnsRetrievalError, error);
            throw new Error(`Failed to get the FQDNs from the kubeconfig: ${error.message}`);
        }
    }

    public async getServicesAsync(namespace: string = null): Promise<IKubernetesService[]> {
        const kubeconfigPath: string = await this._accountContextManager.getKubeconfigPathAsync();
        const args: string[] = [ `get`, `services`, `-o`, `json` ];
        args.push(...(namespace != null ? [ `-n`, namespace ] : [ `--all-namespaces` ]));
        const kubectlOutput: string = await this.runKubectlCommandAsync(args, kubeconfigPath);

        const servicesItems: any[] = JSON.parse(kubectlOutput).items;
        const services: IKubernetesService[] = [];
        for (const serviceItem of servicesItems) {
            if (this.isSystemNamespace(serviceItem.metadata.namespace) || serviceItem.spec.selector == null) {
                continue;
            }

            if (serviceItem.metadata.name === `routingmanager-service`
                || (serviceItem.metadata.labels && serviceItem.metadata.labels[this.ClonedFromIngressMetadataLabel] === `true`)) {
                // Ignore the routing manager service and any cloned service it generated.
                continue;
            }

            services.push({
                name: serviceItem.metadata.name,
                namespace: serviceItem.metadata.namespace,
                selector: serviceItem.spec.selector
            });
        }
        return services;
    }

    public async getIngressesAsync(namespace: string, kubeconfigPath: string, quiet: boolean): Promise<IKubernetesIngress[]> {
        const args: string[] = [ `get`, `ingress`, `-n`, namespace, `-o`, `json` ];
        const kubectlOutput: string = await this.runKubectlCommandAsync(args, kubeconfigPath, quiet);

        const ingressesItems: any[] = JSON.parse(kubectlOutput).items;
        const ingresses: IKubernetesIngress[] = [];
        for (const ingressItem of ingressesItems) {
            if (ingressItem == null || ingressItem.spec == null || ingressItem.spec.rules == null) {
                continue;
            }

            const ingressRules: any[] = ingressItem.spec.rules;

            try {
                for (const ingressRule of ingressRules) {
                    // Ignoring this ingresses of ACME HTTP01 challenge
                    if (ingressRule == null || ingressRule.host == null || ingressRule.http == null || ingressRule.http.paths == null) {
                        continue;
                    }
                    const usesHttps: boolean = ingressItem.spec.tls != null && ingressItem.spec.tls.find(item => item != null && item.hosts != null && item.hosts.indexOf(ingressRule.host) > -1) != null;

                    for (const path of ingressRule.http.paths) {
                        if (path == null || path.backend == null || path.backend.serviceName == null) {
                            continue;
                        }

                        // Empty path should default to "/"
                        if (path.path == null) {
                            path.path = `/`;
                        }

                        // Check if the path is for an ACME challenge in case of HTTPS let's encrypt
                        if (path.path.search(/.well-known\/acme-challenge\//) > -1) {
                            return ingresses;
                        }
                        ingresses.push({
                            name: ingressItem.metadata.name,
                            namespace: ingressItem.metadata.namespace,
                            host: ingressRule.host,
                            protocol: usesHttps ? `https` : `http`,
                            clonedFromName: ingressItem.metadata.annotations[this.ClonedFromAnnotationName]
                        });
                    }
                }
            }
            catch (error) {
                const userFriendlyError = new Error(`Couldn't retrieve the Ingress data from the kubectl command output: ${error.message}`);
                this._logger.error(TelemetryEvent.UnexpectedError, userFriendlyError);
                throw userFriendlyError;
            }
        }
        return ingresses;
    }

    public async getLoadBalancerIngressesAsync(namespace: string, kubeconfigPath: string, quiet: boolean): Promise<IKubernetesIngress[]> {
        const args: string[] = [ `get`, `services`, `-n`, namespace, `-o`, `json` ];
        const kubectlOutput: string = await this.runKubectlCommandAsync(args, kubeconfigPath, quiet);

        const servicesItems: any[] = JSON.parse(kubectlOutput).items;
        const loadBalancerIngresses: IKubernetesIngress[] = [];
        for (const serviceItem of servicesItems) {
            if (serviceItem == null || serviceItem.status == null || serviceItem.status.loadBalancer == null || serviceItem.status.loadBalancer.ingress == null) {
                continue;
            }
            const ingresses: any[] = serviceItem.status.loadBalancer.ingress;

            try {
                for (const ingress of ingresses) {
                    let ingressIp: string = ingress.ip;
                    // If no ingress IP is set, but that an hostname is available, use it to lookup the actual IP.
                    if (ingressIp == null && ingress.hostname != null) {
                        const lookupAddress: dns.LookupAddress = await dns.promises.lookup(ingress.hostname, /*family*/ 4);
                        ingressIp = lookupAddress.address;
                    }

                    if (ingressIp != null) {
                        loadBalancerIngresses.push({
                            name: serviceItem.metadata.name,
                            namespace: serviceItem.metadata.namespace,
                            host: ingressIp,
                            protocol: `http`,
                            clonedFromName: null
                        });
                    }
                }
            }
            catch (error) {
                const userFriendlyError = new Error(`Couldn't retrieve the load balancer Ingress data from the kubectl command output: ${error.message}`);
                this._logger.error(TelemetryEvent.UnexpectedError, userFriendlyError);
                throw userFriendlyError;
            }
        }
        return loadBalancerIngresses;
    }

    public async getNamespacesAsync(kubeconfigPath: string): Promise<string[]> {
        try {
            const args: string[] = [ `get`, `namespaces`, `-o`, `jsonpath={.items[*].metadata.name}` ];
            const kubectlOutput: string = await this.runKubectlCommandAsync(args, kubeconfigPath);
            const namespaces: string[] = kubectlOutput.split(` `);
            return namespaces;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.KubectlClient_GetNamespacesError, error);
            throw new Error(`Failed to retrieve the namespaces: ${error.message}`);
        }
    }

    public isSystemNamespace(namespace: string): boolean {
        return namespace === `azds` || namespace === `azure-system` || namespace === `kube-public` || namespace === `kube-system` || namespace === `kube-node-lease`;
    }

    public async getVersionAsync(): Promise<string> {
        try {
            // Adding retries to ensure the kubectl client is intialized properly
            // as this command is executed right after the download.
            const getVersionAsyncFn = async (): Promise<string> => {
                const args: string[] = [ `version`, `--short`, `--client`, `-o`, `json` ];
                const kubectlOutput: string = await this.runKubectlCommandAsync(args, /*kubeconfigPath*/ null, true);
                const versionJson: object = JSON.parse(kubectlOutput);
                let version: string = versionJson[`clientVersion`][`gitVersion`]; // Example: v1.16.8
                version = version.replace(/v/g, ``); // Remove the occurence of 'v' from the version
                this._logger.trace(TelemetryEvent.KubectlClient_GetVersionSuccess);

                return version;
            };
            return await RetryUtility.retryAsync<string>(getVersionAsyncFn, /*retries*/3, /*delayInMs*/100);
        }
        catch (error) {
            this._logger.error(TelemetryEvent.KubectlClient_GetVersionError, error);
            throw new Error(`Failed to retrieve kubectl version: ${error.message}`);
        }
    }

    public getExecutablePath(): string {
        return this._executablePath;
    }

    private async runKubectlCommandAsync(args: string[], kubeconfigPath: string, quiet: boolean = false): Promise<string> {
        // Run the kubectl command.
        this._logger.trace(TelemetryEvent.KubectlClient_Command, {
            args: args.join(` `)
        });

        // We do not add the kubeconfigPath to args, as it is PII and we don't want it to be logged.
        let argsWithKubeconfig: string[] = [ ...args ];
        if (kubeconfigPath != null) {
            argsWithKubeconfig = argsWithKubeconfig.concat([ `--kubeconfig`, kubeconfigPath ]);
        }

        try {
            const outputData: string = await this._commandRunner.runAsync(
                this._executablePath,
                argsWithKubeconfig,
                /*currentWorkingDirectory*/ null,
                /*customEnvironmentVariables*/ null,
                /*detached*/ false,
                quiet
            );

            this._logger.trace(TelemetryEvent.KubectlClient_CommandSuccess, {
                args: args.join(` `)
            });

            return outputData;
        }
        catch (error) {
            this._logger.error(TelemetryEvent.KubectlClient_CommandError, error, {
                args: args.join(` `)
            });
            throw error;
        }
    }
}