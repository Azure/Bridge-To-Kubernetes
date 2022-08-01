// ----------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ----------------------------------------------------------------------------
'use strict';

import * as assert from 'assert';
import { It, Mock } from 'typemoq';

import { CommandRunner } from '../../clients/CommandRunner';
import { KubectlClient } from '../../clients/KubectlClient';
import { IKubernetesIngress } from '../../models/IKubernetesIngress';
import { IKubernetesService } from '../../models/IKubernetesService';
import { accountContextManagerMock, loggerMock } from '../CommonTestObjects';

suite(`KubectlClient Test`, () => {
    test(`getIngressesAsync when the kubectl command returns a set of various ingresses`, async () => {
        const commandRunnerMock = Mock.ofType<CommandRunner>();
        commandRunnerMock.setup(x => x.runAsync(It.isAnyString(), It.isAny(), It.isAny(), It.isAny(), It.isAny(), It.isAny())).returns(async () => `{
            "apiVersion": "v1",
            "items": [
                {
                    "apiVersion": "extensions/v1beta1",
                    "kind": "Ingress",
                    "metadata": {
                        "annotations": {
                            "kubernetes.io/ingress.class": "traefik",
                            "meta.helm.sh/release-name": "bikesharingsampleapp",
                            "meta.helm.sh/release-namespace": "dev"
                        },
                        "creationTimestamp": "2020-05-12T01:02:49Z",
                        "generation": 1,
                        "labels": {
                            "app": "bikesharingweb",
                            "app.kubernetes.io/managed-by": "Helm",
                            "chart": "bikesharingweb-0.1.0",
                            "heritage": "Helm",
                            "release": "bikesharingsampleapp"
                        },
                        "name": "bikesharingweb",
                        "namespace": "dev",
                        "resourceVersion": "1314825",
                        "selfLink": "/apis/extensions/v1beta1/namespaces/dev/ingresses/bikesharingweb",
                        "uid": "8044fe48-4e8c-454b-b8de-553d0988666e"
                    },
                    "spec": {
                        "rules": [
                            {
                                "host": "dev.bikesharingweb.j7l6v4gz8d.eus.mindaro.io",
                                "http": {
                                    "paths": [
                                        {
                                            "backend": {
                                                "serviceName": "bikesharingweb",
                                                "servicePort": "http"
                                            },
                                            "path": "/"
                                        }
                                    ]
                                }
                            }
                        ]
                    },
                    "status": {
                        "loadBalancer": {
                            "ingress": [
                                {
                                    "ip": "13.72.80.227"
                                }
                            ]
                        }
                    }
                },
                {
                    "apiVersion": "extensions/v1beta1",
                    "kind": "Ingress",
                    "metadata": {
                        "annotations": {
                            "kubernetes.io/ingress.class": "traefik",
                            "meta.helm.sh/release-name": "bikesharingsampleapp",
                            "meta.helm.sh/release-namespace": "dev"
                        },
                        "creationTimestamp": "2020-05-12T01:02:49Z",
                        "generation": 1,
                        "labels": {
                            "app": "gateway",
                            "app.kubernetes.io/managed-by": "Helm",
                            "chart": "gateway-0.1.0",
                            "heritage": "Helm",
                            "release": "bikesharingsampleapp"
                        },
                        "name": "gateway",
                        "namespace": "dev",
                        "resourceVersion": "1314824",
                        "selfLink": "/apis/extensions/v1beta1/namespaces/dev/ingresses/gateway",
                        "uid": "0b61f6fa-f6ad-4a01-b1b2-89255bed41ca"
                    },
                    "spec": {
                        "rules": [
                            {
                                "host": "dev.gateway.j7l6v4gz8d.eus.mindaro.io",
                                "http": {
                                    "paths": [
                                        {
                                            "backend": {
                                                "serviceName": "gateway",
                                                "servicePort": "http"
                                            },
                                            "path": "/"
                                        }
                                    ]
                                }
                            }
                        ]
                    },
                    "status": {
                        "loadBalancer": {
                            "ingress": [
                                {
                                    "ip": "13.72.80.227"
                                }
                            ]
                        }
                    }
                }
            ]
        }`);
        const kubectlClient = new KubectlClient(`my/path/kubectl.exe`, commandRunnerMock.object, accountContextManagerMock.object, loggerMock.object);
        const ingresses: IKubernetesIngress[] = await kubectlClient.getIngressesAsync(`dev`, `c:/users/alias/.kube/config`, true);

        assert.strictEqual(ingresses.length, 2);
        assert.strictEqual(ingresses[0].name, `bikesharingweb`);
        assert.strictEqual(ingresses[0].namespace, `dev`);
        assert.strictEqual(ingresses[0].host, `dev.bikesharingweb.j7l6v4gz8d.eus.mindaro.io`);
        assert.strictEqual(ingresses[0].protocol, `http`);
        assert.strictEqual(ingresses[1].name, `gateway`);
        assert.strictEqual(ingresses[1].namespace, `dev`);
        assert.strictEqual(ingresses[1].host, `dev.gateway.j7l6v4gz8d.eus.mindaro.io`);
        assert.strictEqual(ingresses[1].protocol, `http`);
    });

    test(`getIngressesAsync when the kubectl command returns no ingresses`, async () => {
        const commandRunnerMock = Mock.ofType<CommandRunner>();
        commandRunnerMock.setup(x => x.runAsync(It.isAnyString(), It.isAny(), It.isAny(), It.isAny(), It.isAny(), It.isAny())).returns(async () => `{
            "items": []
        }`);
        const kubectlClient = new KubectlClient(`my/path/kubectl.exe`, commandRunnerMock.object, accountContextManagerMock.object, loggerMock.object);
        const ingresses: IKubernetesIngress[] = await kubectlClient.getIngressesAsync(`dev`, `c:/users/alias/.kube/config`, true);

        assert.strictEqual(ingresses.length, 0);
    });

    test(`getServicesAsync when the kubectl command returns a set of various services`, async () => {
        const commandRunnerMock = Mock.ofType<CommandRunner>();
        commandRunnerMock.setup(x => x.runAsync(It.isAnyString(), It.isAny(), It.isAny(), It.isAny(), It.isAny(), It.isAny())).returns(async () => `{
            "items": [
                {
                    "metadata": {
                        "name": "bikes",
                        "namespace": "dev"
                    },
                    "spec": {
                        "selector": {
                            "app": "bikes",
                            "release": "bikesharing"
                        }
                    }
                },
                {
                    "metadata": {
                        "name": "routingmanager-service",
                        "namespace": "dev"
                    },
                    "spec": {
                        "selector": {
                            "app": "routingmanager-service",
                            "release": "routingmanager"
                        }
                    }
                },
                {
                    "metadata": {
                        "name": "bikesharingweb",
                        "namespace": "dev"
                    },
                    "spec": {
                        "selector": {
                            "app": "bikesharingweb",
                            "release": "bikesharing"
                        }
                    }
                },
                {
                    "metadata": {
                        "labels": {
                            "routing.visualstudio.io/generated": "true"
                        },
                        "name": "bikesharingwebclone",
                        "namespace": "dev"
                    },
                    "spec": {
                        "selector": {
                            "app": "bikesharingwebclone",
                            "release": "bikesharing"
                        }
                    }
                }
            ]
        }`);
        const kubectlClient = new KubectlClient(`my/path/kubectl.exe`, commandRunnerMock.object, accountContextManagerMock.object, loggerMock.object);
        const services: IKubernetesService[] = await kubectlClient.getServicesAsync();

        assert.strictEqual(services.length, 2);
        assert.strictEqual(services[0].name, `bikes`);
        assert.strictEqual(services[0].namespace, `dev`);
        assert.strictEqual(services[0].selector[`app`], `bikes`);
        assert.strictEqual(services[0].selector[`release`], `bikesharing`);
        assert.strictEqual(services[1].name, `bikesharingweb`);
        assert.strictEqual(services[1].namespace, `dev`);
        assert.strictEqual(services[1].selector[`app`], `bikesharingweb`);
        assert.strictEqual(services[1].selector[`release`], `bikesharing`);
    });

    test(`getServicesAsync when the kubectl command returns services in system namespaces`, async () => {
        const commandRunnerMock = Mock.ofType<CommandRunner>();
        commandRunnerMock.setup(x => x.runAsync(It.isAnyString(), It.isAny(), It.isAny(), It.isAny(), It.isAny(), It.isAny())).returns(async () => `{
            "items": [
                {
                    "metadata": {
                        "name": "azds-webhook-service",
                        "namespace": "azds"
                    },
                    "spec": {
                        "selector": {
                            "component": "azds-injector-webhook",
                            "service": "azds-webhook-service"
                        }
                    }
                },
                {
                    "metadata": {
                        "name": "kube-public-service",
                        "namespace": "kube-public"
                    }
                },
                {
                    "metadata": {
                        "name": "bikes",
                        "namespace": "dev"
                    },
                    "spec": {
                        "selector": {
                            "app": "bikes",
                            "release": "bikesharing"
                        }
                    }
                },
                {
                    "metadata": {
                        "name": "kube-dns",
                        "namespace": "kube-system"
                    },
                    "spec": {
                        "selector": {
                            "k8s-app": "kube-dns"
                        }
                    }
                }
            ]
        }`);
        const kubectlClient = new KubectlClient(`my/path/kubectl.exe`, commandRunnerMock.object, accountContextManagerMock.object, loggerMock.object);
        const services: IKubernetesService[] = await kubectlClient.getServicesAsync();

        // Validate that the services in system namespaces have been filtered out properly.
        assert.strictEqual(services.length, 1);
        assert.strictEqual(services[0].name, `bikes`);
        assert.strictEqual(services[0].namespace, `dev`);
        assert.strictEqual(services[0].selector[`app`], `bikes`);
        assert.strictEqual(services[0].selector[`release`], `bikesharing`);
    });

    test(`getServicesAsync when the kubectl command returns no services`, async () => {
        const commandRunnerMock = Mock.ofType<CommandRunner>();
        commandRunnerMock.setup(x => x.runAsync(It.isAnyString(), It.isAny(), It.isAny(), It.isAny(), It.isAny(), It.isAny())).returns(async () => `{
            "items": []
        }`);
        const kubectlClient = new KubectlClient(`my/path/kubectl.exe`, commandRunnerMock.object, accountContextManagerMock.object, loggerMock.object);
        const services: IKubernetesService[] = await kubectlClient.getServicesAsync();

        assert.strictEqual(services.length, 0);
    });

    test(`getNamespacesAsync when the kubectl command returns a set of various namespacess`, async () => {
        const commandRunnerMock = Mock.ofType<CommandRunner>();
        commandRunnerMock.setup(x => x.runAsync(It.isAnyString(), It.isAny(), It.isAny(), It.isAny(), It.isAny(), It.isAny())).returns(async () => `default kube-node-lease voting-app`);
        const kubectlClient = new KubectlClient(`my/path/kubectl.exe`, commandRunnerMock.object, accountContextManagerMock.object, loggerMock.object);
        const namespaces: string[] = await kubectlClient.getNamespacesAsync(`c:/users/alias/.kube/config`);

        assert.strictEqual(namespaces.length, 3);
        assert.strictEqual(namespaces[0], `default`);
        assert.strictEqual(namespaces[1], `kube-node-lease`);
        assert.strictEqual(namespaces[2], `voting-app`);
    });
});