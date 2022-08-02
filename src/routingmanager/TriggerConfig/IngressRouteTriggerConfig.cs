// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using k8s.Models;

namespace Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig
{
    internal class IngressRouteTriggerConfig : ITriggerConfig
    {
        public IngressRouteTriggerConfig(
            string namespaceName,
            V1Service triggerService,
            string ingressRouteName,
            int servicePort,
            string host,
            V1Probe httpReadinessProbe,
            V1Probe httpLivenessProbe)
        {
            NamespaceName = string.IsNullOrWhiteSpace(namespaceName) ? throw new ArgumentNullException(nameof(namespaceName)) : namespaceName;
            TriggerService = triggerService ?? throw new ArgumentNullException(nameof(triggerService));
            TriggerEntityName = string.IsNullOrWhiteSpace(ingressRouteName) ? throw new ArgumentNullException(nameof(ingressRouteName)) : ingressRouteName;
            ServicePort = servicePort;
            Host = host ?? string.Empty;
            HttpReadinessProbe = httpReadinessProbe;
            HttpLivenessProbe = httpLivenessProbe;
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
        /// Ingress name
        /// </summary>
        public string TriggerEntityName { get; }

        /// <summary>
        /// Service port for this ingress trigger
        /// </summary>
        public int ServicePort { get; }

        /// <summary>
        /// Virtual host for this ingress route trigger - comes from virtual host property in the yaml
        /// </summary>
        /// <remarks>
        /// virtualhost appears at most once. If it is present, the object is considered
        /// to be a "root". It is the fully qualified domain name of the root of the ingress tree
        /// All leaves of the DAG (directed acyclic graph) rooted at this object relate to the fqdn
        /// </remarks>
        public string Host { get; }

        /// <summary>
        /// Remote agent readiness probe
        /// </summary>
        public V1Probe HttpReadinessProbe { get; }

        /// <summary>
        /// Remote agent liveness probe
        /// </summary>
        public V1Probe HttpLivenessProbe { get; }
    }
}