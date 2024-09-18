// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using k8s.Models;

namespace Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig
{
    /// <summary>
    /// Trigger config for an LPK agent pod
    /// </summary>
    internal class PodTriggerConfig : ITriggerConfig
    {
        public PodTriggerConfig(
            string namespaceName,
            V1Service triggerService,
            string lpkPodName,
            string routeOnHeaderKey,
            string routeOnHeaderValue,
            string triggerPodIP,
            string correlationId,
            string routeUniqueName
            )
        {
            NamespaceName = string.IsNullOrWhiteSpace(namespaceName) ? throw new ArgumentNullException(nameof(namespaceName)) : namespaceName;
            TriggerService = triggerService ?? throw new ArgumentNullException(nameof(triggerService));
            TriggerEntityName = string.IsNullOrWhiteSpace(lpkPodName) ? throw new ArgumentNullException(nameof(lpkPodName)) : lpkPodName;
            RouteOnHeader = string.IsNullOrWhiteSpace(routeOnHeaderKey) ?
                                throw new ArgumentNullException(nameof(routeOnHeaderKey)) : string.IsNullOrWhiteSpace(routeOnHeaderKey) ?
                                                                                                throw new ArgumentNullException(nameof(routeOnHeaderValue)) : new KeyValuePair<string, string>(routeOnHeaderKey, routeOnHeaderValue);
            TriggerPodIp = triggerPodIP ?? throw new ArgumentNullException(nameof(triggerPodIP));
            TriggerEntityName = string.IsNullOrWhiteSpace(lpkPodName) ? throw new ArgumentNullException(nameof(lpkPodName)) : lpkPodName;
            CorrelationId = correlationId;
            RouteUniqueName = routeUniqueName;
        }

        /// <summary>
        /// <see cref="ITriggerConfig.NamespaceName"/>
        /// </summary>
        public string NamespaceName { get; }

        /// <summary>
        /// <see cref="ITriggerConfig.TriggerService"/>
        /// </summary>
        public V1Service TriggerService { get; }

        /// <summary>
        /// kubernetes-route-on header key and value
        /// </summary>
        public KeyValuePair<string, string> RouteOnHeader { get; }

        /// <summary>
        /// LPK agent pod name
        /// </summary>
        public string TriggerEntityName { get; }
        
        /// <summary>
        /// the unique name of the pod used to expose it via a service
        /// </summary>
        public string RouteUniqueName { get; }

        /// <summary>
        /// LPK agent Pod IP
        /// </summary>
        public string TriggerPodIp { get; }

        /// <summary>
        /// The CorrelationId label value. This is set by the client when deploying a new pod in isolation.
        /// </summary>
        public string CorrelationId { get; }
    }
}