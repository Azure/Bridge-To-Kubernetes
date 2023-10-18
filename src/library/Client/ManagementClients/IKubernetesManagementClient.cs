// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.Client.ManagementClients
{
    /// <summary>
    /// Handles a specific Kubernetes cluster
    /// </summary>
    public interface IKubernetesManagementClient : IDisposable
    {
        /// <summary>
        /// Runs a long running kubectl command and output the <see cref="Models.AuthentionTarget"/> to be used to refresh the credentials
        /// Once the user completes the login this method returns successfully, if something goes wrong an exception is thrown.
        /// </summary>
        /// <param name="targetNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task RefreshCredentialsAsync(string targetNamespace, CancellationToken cancellationToken);

        /// <summary>
        /// Check if a refresh of the token in the kubeconfig is needed. This is usually required for AAD authentication on AKS clusters or other OAuth flows
        /// </summary>
        /// <param name="targetNamespace"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<bool> CheckCredentialsAsync(string targetNamespace, CancellationToken cancellationToken);

        /// <summary>
        /// Lists all the namespaces in a Kubernetes cluster
        /// </summary>
        /// <param name="excludeReservedNamespaces">Optionally filter out known reserved namespaces</param>
        /// <returns>A list of namespaces in a Kubernetes cluster</returns>
        Task<OperationResponse<IEnumerable<string>>> ListNamespacesAsync(CancellationToken cancellationToken, bool excludeReservedNamespaces = true);

        /// <summary>
        /// Lists all the Services in the specified namespace
        /// </summary>
        /// <param name="namespaceName"> The name of the namespace where to look for services</param>
        /// <param name="excludeSystemServices">Optionally filter out known system services</param>
        /// <returns></returns>
        Task<OperationResponse<IEnumerable<string>>> ListServicesInNamespacesAsync(string namespaceName, CancellationToken cancellationToken, bool excludeSystemServices = true);

        /// <summary>
        /// Lists all the public Urls in the specified namespace
        /// </summary>
        /// <param name="namespaceName">The name of the namespace where to look for ingresses</param>
        /// <param name="routingHeaderValue">The value of the routing header to filter ingresses for routing scenarios</param>
        /// <returns></returns>
        Task<OperationResponse<IEnumerable<Uri>>> ListPublicUrlsInNamespaceAsync(string namespaceName, CancellationToken cancellationToken, string routingHeaderValue = null);

        #region Routing methods

        /// <summary>
        /// Fetches subdomain header for routing purposes.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns> Subdomain to be used as a header for ingresses when using Connect with routing. </returns>
        OperationResponse<string> GetRoutingHeader(CancellationToken cancellationToken);

        /// <summary>
        /// If cluster is Dev Spaces enabled, routing is NOT supported.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns> Returns true if cluster supports routing. </returns>
        Task<OperationResponse<bool>> IsRoutingSupportedAsync(CancellationToken cancellationToken);

        #endregion Routing methods
    }
}