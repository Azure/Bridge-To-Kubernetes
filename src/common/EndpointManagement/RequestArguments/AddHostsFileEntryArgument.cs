// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// HostsFileEntry describes a line in hosts file
    /// </summary>
    public class AddHostsFileEntryArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// IP address
        /// </summary>
        [JsonPropertyName("workloadNamespace")]
        public string WorkloadNamespace { get; set; }

        /// <summary>
        /// Host names
        /// </summary>
        [JsonPropertyName("entries")]
        public IEnumerable<HostsFileEntry> Entries { get; set; }
    }
}
