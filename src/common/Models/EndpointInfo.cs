// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;
using Microsoft.BridgeToKubernetes.Common.Models.Settings;

namespace Microsoft.BridgeToKubernetes.Common.Models
{
    /// <summary>
    /// EndpointInfo describes any service in the cluster, or any endpoint outside of the cluster, which the local process needs to communicate with via the devhostagent.
    /// </summary>
    public class EndpointInfo
    {
        /// <summary>
        /// Name of the service or endpoint
        /// </summary>
        /// <remarks>This is the DNS name that the Remote Agent will direct traffic to in the cluster</remarks>
        public string DnsName { get; set; }

        /// <summary>
        /// The local IP assigned to the endpoint
        /// </summary>
        public IPAddress LocalIP { get; set; }

        /// <summary>
        /// Ports used to communicate with the endpoint
        /// </summary>
        public PortPair[] Ports { get; set; }

        /// <summary>
        /// True if this endpoint info is targeting an endpoint outside of the cluster. False otherwise.
        /// </summary>
        public bool IsExternalEndpoint { get; set; } = false;

        /// <summary>
        /// Indicates if the endpoint is in the same namespace as the user's workload
        /// </summary>
        public bool IsInWorkloadNamespace { get; set; } = true;
    }
}