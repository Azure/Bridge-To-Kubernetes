// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.EndpointManagement;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.ClientFactory
{
    public interface IManagementClientFactory
    {
        /// <summary>
        /// Creates a kubeconfig management client.
        /// </summary>
        IKubeConfigManagementClient CreateKubeConfigClient(string targetKubeConfigContext = null);

        /// <summary>
        /// Creates a Kubernetes management client.
        /// </summary>
        IKubernetesManagementClient CreateKubernetesManagementClient(KubeConfigDetails kubeConfigDetails);

        /// <summary>
        /// Creates a connect management client
        /// </summary>
        IConnectManagementClient CreateConnectManagementClient(
            RemoteContainerConnectionDetails containerIdentifier,
            KubeConfigDetails kubeConfigDetails,
            bool useKubernetesServiceEnvironmentVariables,
            bool runContainerized);

        /// <summary>
        /// Creates a routing management client.
        /// </summary>
        IRoutingManagementClient CreateRoutingManagementClient(
            string namespaceName,
            KubeConfigDetails kubeConfigDetails);

        /// <summary>
        /// Creates an EndpointManager client.
        /// </summary>
        IEndpointManagementClient CreateEndpointManagementClient();
    }
}