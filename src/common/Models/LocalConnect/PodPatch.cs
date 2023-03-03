// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using System.Text.Json.Serialization;
using SystemTextJsonPatch;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Object model that contains information related to a patched pod
    /// </summary>
    internal class PodPatch : PatchEntityBase
    {
        [JsonConstructor]
        public PodPatch(V1Pod pod, JsonPatchDocument<V1Pod> reversePatch)
        {
            this.Pod = pod;
            this.ReversePatch = reversePatch;
        }

        public override KubernetesResourceType KubernetesType => KubernetesResourceType.Pod;
        public override string Namespace => this.Pod?.Namespace();
        public override string Name => this.Pod?.Name();

        /// <summary>
        /// The pod that was patched
        /// </summary>
        [JsonPropertyName("pod")]
        public V1Pod Pod { get; private set; }

        /// <summary>
        /// The operations to perform to reverse the patch
        /// </summary>
        [JsonPropertyName("reversePatch")]
        public JsonPatchDocument<V1Pod> ReversePatch { get; private set; }
    }
}