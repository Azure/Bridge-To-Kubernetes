// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using k8s.Models;

namespace Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig
{
    /// <summary>
    /// Trigger config for a particular host & path in an ingress
    /// </summary>
    internal class IngressTriggerConfig : ITriggerConfig
    {
        public IngressTriggerConfig(
            string namespaceName,
            V1Service triggerService,
            string ingressName,
            int servicePort,
            string host,
            bool isAgicIngress,
            string agicBackendHostname,
            V1Probe httpReadinessProbe,
            V1Probe httpLivenessProbe)
        {
            NamespaceName = string.IsNullOrWhiteSpace(namespaceName) ? throw new ArgumentNullException(nameof(namespaceName)) : namespaceName;
            TriggerService = triggerService ?? throw new ArgumentNullException(nameof(triggerService));
            TriggerEntityName = string.IsNullOrWhiteSpace(ingressName) ? throw new ArgumentNullException(nameof(ingressName)) : ingressName;
            ServicePort = servicePort;
            Host = host ?? string.Empty;
            IsAgicIngress = isAgicIngress;
            AgicBackendHostName = agicBackendHostname ?? string.Empty;
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
        /// Host for this ingress trigger - comes from host property on ingress yaml
        /// </summary>
        /// <remarks>
        /// If this value is null/empty, it means this trigger is for all incoming http traffic
        /// </remarks>
        public string Host { get; }

        /// <summary>
        /// If it is supported by Azure Gateway Ingress Controller (Agic)
        /// i.e. if it has the annotation kubernetes.io/ingress.class: azure/application-gateway
        /// </summary>
        public bool IsAgicIngress { get; }

        /// <summary>
        /// This value comes from the annotation appgw.ingress.kubernetes.io/backend-hostname when user is using
        /// Azure Application Gateway Ingress Controller. When this annotation is set on the ingress,
        /// AGIC calls the pod directly with host set as value if annotation. This is why we need to
        /// add envoy rules for this host as well.
        /// More details on AGIC:
        /// 1. https://github.com/Azure/application-gateway-kubernetes-ingress
        /// 2. https://github.com/Azure/application-gateway-kubernetes-ingress/blob/master/docs/annotations.md#backend-hostname
        /// </summary>
        public string AgicBackendHostName { get; } = string.Empty;

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