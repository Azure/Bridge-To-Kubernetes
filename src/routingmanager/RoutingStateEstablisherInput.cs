// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    /// <summary>
    /// Input for the class <see cref="RoutingStateEstablisher"/>
    /// Contains the pod triggers, ingress triggers and load balancer service trigger associated with a particular service
    /// </summary>
    internal class RoutingStateEstablisherInput
    {
        public RoutingStateEstablisherInput(
            List<PodTriggerConfig> podTriggers = null,
            List<IngressTriggerConfig> ingressTriggers = null,
            List<IngressRouteTriggerConfig> ingressRouteTriggers = null,
            LoadBalancerTriggerConfig loadBalancerTrigger = null)
        {
            PodTriggers = podTriggers ?? new List<PodTriggerConfig>();
            IngressTriggers = ingressTriggers ?? new List<IngressTriggerConfig>();
            IngressRouteTriggers = ingressRouteTriggers ?? new List<IngressRouteTriggerConfig>();
            LoadBalancerTrigger = loadBalancerTrigger;
        }

        /// <summary>
        /// List of <see cref="PodTriggerConfig"/>
        /// </summary>
        public List<PodTriggerConfig> PodTriggers { get; }

        /// <summary>
        /// List of <see cref="IngressTriggerConfig"/>
        /// </summary>
        public List<IngressTriggerConfig> IngressTriggers { get; }

        /// <summary>
        /// List of <see cref="IngressRouteTriggerConfig"/>
        /// </summary>
        public List<IngressRouteTriggerConfig> IngressRouteTriggers { get; }

        /// <summary>
        /// <see cref="LoadBalancerTriggerConfig"/>
        /// </summary>
        public LoadBalancerTriggerConfig LoadBalancerTrigger { get; private set; }

        public void AddLoadBalancerTrigger(LoadBalancerTriggerConfig loadBalancerTrigger)
        {
            LoadBalancerTrigger = loadBalancerTrigger;
        }
    }
}