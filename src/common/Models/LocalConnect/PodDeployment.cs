// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Object model that contains information related to a newly deployed pod
    /// </summary>
    internal class PodDeployment : PatchEntityBase
    {
        [JsonConstructor]
        public PodDeployment(V1Pod pod)
        {
            this.Pod = pod;
        }

        public override KubernetesResourceType KubernetesType => KubernetesResourceType.Pod;
        public override string Namespace => this.Pod?.Namespace();
        public override string Name => this.Pod?.Name();

        /// <summary>
        /// The pod that was deployed
        /// </summary>
        [JsonPropertyName("pod")]
        public V1Pod Pod { get; private set; }

        /// <summary>
        /// The user pod that was replaced, if any
        /// </summary>
        [JsonPropertyName("userPodToRestore")]
        public V1Pod UserPodToRestore { get; set; }
    }
}