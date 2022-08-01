// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    /// <summary>
    /// Represents a client for watching Kubernetes objects.
    /// The official .NET client library is unable to properly implement
    /// watching of objects due to how AutoRest currently generates code
    /// according to the Swagger for the Kubernetes REST API.
    /// </summary>
    internal interface IKubernetesWatcher : IDisposable
    {
        /// <summary>
        /// Watches for changes in the set of Kubernetes namespaces.
        /// Returned Task completes when the watch is cancelled.
        /// </summary>
        Task WatchNamespacesAsync(
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken);

        /// <summary>
        /// Watches for changes in the set of Kubernetes service objects.
        /// Returned Task completes when the watch is cancelled.
        /// </summary>
        Task WatchServicesAsync(
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken);

        /// <summary>
        /// Watches for changes in the set of Kubernetes ingresse objects.
        /// Returned Task completes when the watch is cancelled.
        /// </summary>
        Task WatchIngressesAsync(
            string namespaceName,
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken);

        /// <summary>
        /// Watches for changes in the set of Kubernetes ingresse objects.
        /// Returned Task completes when the watch is cancelled.
        /// </summary>
        Task WatchPodsAsync(
            string namespaceName,
            Action<WatchEventType, V1ObjectMeta> callback,
            CancellationToken cancellationToken);
    }
}