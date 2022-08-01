// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;

namespace Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig
{
    /// <summary>
    /// Configuration that captures the trigger details for routing manager.
    /// Routing Manager can be triggered by
    /// 1. Any ingress in the namespace
    /// 2. Pod with label <see cref="Common.Constants.Routing.RouteFromLabelName"/> and annotation <see cref="Common.Constants.Routing.RouteOnHeaderAnnotationName"/>
    /// </summary>
    internal interface ITriggerConfig
    {
        /// <summary>
        /// Namespace that Routing manager watches and acts on
        /// </summary>
        public string NamespaceName { get; }

        /// <summary>
        /// Service associated with the trigger
        /// </summary>
        public V1Service TriggerService { get; }

        /// <summary>
        /// Pod or ingress name that acts as a trigger
        /// </summary>
        public string TriggerEntityName { get; }
    }
}