// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;

namespace Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig
{
    /// <summary>
    /// Trigger config for services of type load balancer
    /// </summary>
    public class LoadBalancerTriggerConfig : ITriggerConfig
    {
        public LoadBalancerTriggerConfig(string namespaceName, V1Service triggerService, string triggerEntityName, bool isIngressController)
        {
            NamespaceName = namespaceName;
            TriggerService = triggerService;
            TriggerEntityName = triggerEntityName;
            IsIngressController = isIngressController;
        }

        public string NamespaceName { get; }

        public V1Service TriggerService { get; }

        public string TriggerEntityName { get; }

        /// <summary>
        /// This property is true if this load balancer is a part of ingress controller
        /// </summary>
        public bool IsIngressController { get; }
    }
}