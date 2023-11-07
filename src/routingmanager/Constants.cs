// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    internal static class Constants
    {
        public const string RoutingSuffix = "-" + "routing";

        // Name suffix for cloned services
        public const string ClonedSuffix = "-cloned" + RoutingSuffix;

        // Name suffix for envoy deployment, pod and config map
        public const string EnvoySuffix = "-envoy" + RoutingSuffix;

        // Label used on generated envoy deployment, pod and config map
        public const string TriggerEntityLabel = Common.Constants.Routing.RoutingLabelPrefix + "triggerEntity";

        // This label is used in user's original service to select over envoy pods
        public const string EntityLabel = Common.Constants.Routing.RoutingLabelPrefix + "entity";

        public const string EnvoyImageName = "envoyproxy/envoy:v1.16.5";

        public const string OriginalServiceSelectorAnnotation = Common.Constants.Routing.RoutingLabelPrefix + "originalServiceSelector";
        public const string ClonedFromAnnotation = Common.Constants.Routing.RoutingLabelPrefix + "clonedFrom";

        public const string CertManagerAnnotationName = "cert-manager.io/cluster-issuer";

        // The below set of popular ingress controller image names are used to determine if a service of type load balancer belongs to an ingress controller
        public static List<string> IngressControllerImageNames =
            new List<string> { "envoyproxy/", "nginx-ingress-controller", "ingress-nginx/controller", "haproxytech/kubernetes-ingress", "nginx/nginx-ingress", "traefik" };

        public const string IngressClassAnnotation = "kubernetes.io/ingress.class";
        public const string IngressClassAnnotationAgicValue = "azure/application-gateway"; // Azure Application Gateway Ingress Controller

        public const string AgicBackendHostnameAnnotation = "appgw.ingress.kubernetes.io/backend-hostname";

        internal static class KubernetesError
        {
            public const string Conflict = "Conflict";
            public const string UnprocessableEntity = "UnprocessableEntity";
        }

        // Matches hosts such as *.abc.com
        public static readonly Regex WildcardHostRegex = new Regex(@"^\*\.([a-zA-Z0-9-]*\.)+.[a-zA-Z0-9-]*$", RegexOptions.Compiled); // Regex for wildcard ingress domains like "*.abc.com", "*.abc.def.com"
    }
}