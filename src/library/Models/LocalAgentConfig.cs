// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Newtonsoft.Json;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    public class LocalAgentConfig
    {
        /// <summary>
        /// Reachable services
        /// </summary>
        [JsonProperty("reachableEndpoints")]
        public IEnumerable<EndpointInfo> ReachableEndpoints { get; set; }

        /// <summary>
        /// The port exposed by the local user workload when running locally
        /// </summary>
        [JsonProperty("reversePortForwardInfo")]
        public IEnumerable<PortForwardStartInfo> ReversePortForwardInfo { get; set; }

        [JsonProperty("remoteAgentInfo")]
        public RemoteAgentInfo RemoteAgentInfo { get; set; }
    }
}