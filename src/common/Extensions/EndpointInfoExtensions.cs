// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Models
{
    internal static class EndpointInfoExtensions
    {
        public static IEnumerable<string> GetServiceAliases(this EndpointInfo endpoint, string workloadNamespace, ILog logger)
        {
            var serviceAliases = new List<string>() { endpoint.DnsName }; // Always add the base name

            if (!endpoint.IsExternalEndpoint)
            {
                // If the entry is not an external endpoint, add all the possible dns extensions
                if (endpoint.IsInWorkloadNamespace)
                {
                    // It's a cluster service in the same namespace as the workload, we need to add the workload namespace to the possible dsn entries
                    serviceAliases.Add($"{endpoint.DnsName}.{workloadNamespace}");
                    serviceAliases.Add($"{endpoint.DnsName}.{workloadNamespace}.svc");
                    serviceAliases.Add($"{endpoint.DnsName}.{workloadNamespace}.svc.cluster.local");
                }
                else
                {
                    // It's a cluster service in a namespace different than the workload, the namespace is already part of the baseDnsName
                    serviceAliases.Add($"{endpoint.DnsName}.svc");
                    serviceAliases.Add($"{endpoint.DnsName}.svc.cluster.local");
                }
            }
            // Replace unqualified service name with all aliases
            return serviceAliases;
        }

        public static void ValidateDnsName(this EndpointInfo endpoint)
        {
            // Validate DnsName
            if (string.IsNullOrEmpty(endpoint.DnsName))
            {
                throw new InvalidOperationException($"Invalid {nameof(endpoint.DnsName)}: service name cannot be null");
            }
            if (Uri.CheckHostName(endpoint.DnsName) == 0)
            {
                throw new InvalidOperationException($"Invalid host name: {endpoint.DnsName}");
            }
        }
    }
}