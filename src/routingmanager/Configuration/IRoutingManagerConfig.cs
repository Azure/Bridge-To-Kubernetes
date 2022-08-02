// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Configuration
{
    /// <summary>
    /// Routing Manager configuration
    /// </summary>
    internal interface IRoutingManagerConfig
    {
        /// <summary>
        /// Get the namespace that Routing Manager will watch
        /// </summary>
        string GetNamespace();
    }
}