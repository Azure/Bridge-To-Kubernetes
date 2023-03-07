// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using k8s;
using k8s.Models;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Traefik
{
    internal class Tls
    {
        public string SecretName { get; set; }
        public string MinimumProtocolVersion { get; set; }
    }

    internal class Virtualhost
    {
        public string Fqdn { get; set; }
        public Tls Tls { get; set; }
    }

    internal class HealthCheckOptional
    {
        public string Path { get; set; }
        public int IntervalSeconds { get; set; }
        public int TimeoutSeconds { get; set; }
        public int UnhealthyThresholdCount { get; set; }
        public int HealthyThresholdCount { get; set; }
        public string Host { get; set; }
    }

    internal class Service
    {
        public string Name { get; set; }
        public int Port { get; set; }
        public int? Weight { get; set; }
        public string Strategy { get; set; }

        [JsonPropertyName("healthCheck(Optional)")]
        public HealthCheckOptional HealthCheckOptional { get; set; }
    }

    internal class Delegate
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
    }

    internal class Route
    {
        public string Kind { get; } = "Rule";
        public string Match { get; set; }
        public List<Service> Services { get; set; }
        public bool PermitInsecure { get; set; }
        public Delegate Delegate { get; set; }
    }

    internal class Spec
    {
        public Virtualhost Virtualhost { get; set; }
        public string Strategy { get; set; }

        [JsonPropertyName("healthCheck(Optional)")]
        public HealthCheckOptional HealthCheckOptional { get; set; }
        public List<Route> Routes { get; set; }
    }

    internal class IngressRoute : IMetadata<V1ObjectMeta>
    {
        public string ApiVersion { get; set; }
        public string Kind { get; set; }
        public V1ObjectMeta Metadata { get; set; }
        public Spec Spec { get; set; }
    }

    internal class IngressRoutes
    {
        public IEnumerable<IngressRoute> Items { get; set; }
    }
}