// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Newtonsoft.Json;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Marker interface for patch state entities
    /// </summary>
    internal abstract class PatchEntityBase
    {
        /// <summary>
        /// The type of patch entity
        /// </summary>
        [JsonProperty("type")]
        public string Type => this.GetType().Name;

        [JsonIgnore]
        public abstract KubernetesResourceType KubernetesType { get; }

        [JsonIgnore]
        public abstract string Namespace { get; }

        [JsonIgnore]
        public abstract string Name { get; }
    }
}