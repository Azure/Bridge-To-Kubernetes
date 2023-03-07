// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// Argument to tell EndpointManager what hosts to add to the HOST file
    /// </summary>
    public class AddHostsFileEntryArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// The namespace
        /// </summary>
        [JsonPropertyName("workloadNamespace")]
        public string WorkloadNamespace { get; set; }

        /// <summary>
        /// Host file entries
        /// </summary>
        [JsonPropertyName("entries")]
        public IEnumerable<HostsFileEntry> Entries { get; set; }
    }
}
