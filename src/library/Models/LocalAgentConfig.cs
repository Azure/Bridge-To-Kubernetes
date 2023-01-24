// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    public class LocalAgentConfig
    {
        /// <summary>
        /// Reachable services
        /// </summary>
        [JsonPropertyName("reachableEndpoints")]
        public IEnumerable<EndpointInfo> ReachableEndpoints { get; set; }

        /// <summary>
        /// The port exposed by the local user workload when running locally
        /// </summary>
        [JsonPropertyName("reversePortForwardInfo")]
        public IEnumerable<PortForwardStartInfo> ReversePortForwardInfo { get; set; }

        [JsonPropertyName("remoteAgentInfo")]
        public RemoteAgentInfo RemoteAgentInfo { get; set; }
    }
}