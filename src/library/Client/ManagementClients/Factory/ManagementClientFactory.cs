// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.EndpointManagement;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.ClientFactory
{
    /// <summary>
    /// Factory to create management clients.
    /// </summary>
    public class ManagementClientFactory : IManagementClientFactory
    {
        private string _userAgent;
        private string _correlationId;

        public ManagementClientFactory(string userAgent, string correlationId)
        {
            this._userAgent = userAgent ?? throw new ArgumentNullException(nameof(userAgent));
            this._correlationId = correlationId ?? throw new ArgumentNullException(nameof(correlationId));
        }

        public static Func<bool> IsTelemetryEnabledCallback  // Entrypoint for VS and CLI to set an isTelemetryEnabled callback to be used for the rest of the client session
        {
            set
            {
                AppContainerConfig.RootScope.Resolve<IApplicationInsightsLoggerConfig>().IsTelemetryEnabledCallback = value;
            }
        }

        public static bool IsLogFileEnabled
        {
            set
            {
                AppContainerConfig.RootScope.Resolve<IFileLoggerConfig>().LogFileEnabled = value;
            }
        }

        /// <summary>
        /// <see cref="IManagementClientFactory.CreateKubeConfigClient()"/>
        /// </summary>
        public IKubeConfigManagementClient CreateKubeConfigClient(string targetKubeConfigContext = null)
        {
            return AppContainerConfig.CreateOwnedObjectFromScope(s => s.Resolve<KubeConfigManagementClient.Factory>()(userAgent: _userAgent, correlationId: _correlationId, targetKubeConfigContext: targetKubeConfigContext));
        }

        /// <summary>
        /// Creates a Kubernetes management client.
        /// </summary>
        public IKubernetesManagementClient CreateKubernetesManagementClient(KubeConfigDetails kubeConfigDetails)
        {
            IKubernetesClient kubernetesClient = CreateKubernetesClientWithContext(kubeConfigDetails);
            return AppContainerConfig.CreateOwnedObjectFromScope(s => s.Resolve<KubernetesManagementClient.Factory>()(userAgent: this._userAgent, correlationId: this._correlationId),
                builder =>
                {
                    builder.RegisterInstance(kubernetesClient)
                           .As<IKubernetesClient>()
                           .SingleInstance();
                });
        }

        public IConnectManagementClient CreateConnectManagementClient(RemoteContainerConnectionDetails containerIdentifier, KubeConfigDetails kubeConfigDetails, bool useKubernetesServiceEnvironmentVariables, bool runContainerized)
        {
            IKubernetesClient kubernetesClient = CreateKubernetesClientWithContext(kubeConfigDetails);
            return AppContainerConfig.CreateOwnedObjectFromScope(s => s.Resolve<ConnectManagementClient.Factory>()(containerConnectionDetails: containerIdentifier, useKubernetesServiceEnvironmentVariables: useKubernetesServiceEnvironmentVariables, runContainerized: runContainerized, userAgent: this._userAgent, correlationId: this._correlationId),
                builder =>
                {
                    builder.RegisterInstance(kubernetesClient)
                           .As<IKubernetesClient>()
                           .SingleInstance();
                });
        }

        public IRoutingManagementClient CreateRoutingManagementClient(string namespaceName, KubeConfigDetails kubeConfigDetails)
        {
            IKubernetesClient kubernetesClient = CreateKubernetesClientWithContext(kubeConfigDetails);
            return AppContainerConfig.CreateOwnedObjectFromScope(s => s.Resolve<RoutingManagementClient.Factory>()(namespaceName: namespaceName, userAgent: this._userAgent, correlationId: this._correlationId),
                builder =>
                {
                    builder.RegisterInstance(kubernetesClient)
                           .As<IKubernetesClient>()
                           .SingleInstance();
                });
        }

        public IEndpointManagementClient CreateEndpointManagementClient()
        {
            return AppContainerConfig.CreateOwnedObjectFromScope(s => s.Resolve<EndpointManagementClient.Factory>()(userAgent: _userAgent, correlationId: _correlationId));
        }

        private IKubernetesClient CreateKubernetesClientWithContext(KubeConfigDetails kubeConfigDetails)
        {
            var kubernetesClientFactory = AppContainerConfig.RootScope.Resolve<KubernetesClient.Factory>();
            return kubernetesClientFactory(kubeConfigDetails.Configuration, kubeConfigDetails.Path);
        }
    }
}