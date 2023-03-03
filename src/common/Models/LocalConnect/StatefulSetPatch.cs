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
    /// Object model that contains information related to a patched statefulSet
    /// </summary>
    internal class StatefulSetPatch : PatchEntityBase
    {
        [JsonConstructor]
        public StatefulSetPatch(V1StatefulSet statefulSet, JsonPatchDocument<V1StatefulSet> reversePatch)
        {
            this.StatefulSet = statefulSet;
            this.ReversePatch = reversePatch;
        }

        public override KubernetesResourceType KubernetesType => KubernetesResourceType.StatefulSet;
        public override string Namespace => this.StatefulSet?.Namespace();
        public override string Name => this.StatefulSet?.Name();

        /// <summary>
        /// The statefulSet that was patched
        /// </summary>
        [JsonPropertyName("statefulSet")]
        public V1StatefulSet StatefulSet { get; private set; }

        /// <summary>
        /// The operations to perform to reverse the patch
        /// </summary>
        [JsonPropertyName("reversePatch")]
        public JsonPatchDocument<V1StatefulSet> ReversePatch { get; private set; }
    }
}