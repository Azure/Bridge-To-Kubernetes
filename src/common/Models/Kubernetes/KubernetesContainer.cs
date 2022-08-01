// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Represents a container running in Kubernetes
    /// </summary>
    public class KubernetesContainer
    {
        /// <summary>
        /// When container was created
        /// </summary>
        public string CreationTimestamp { get; set; }

        /// <summary>
        /// Name of container
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Unique identifier of container
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Image of container
        /// </summary>
        public string Image { get; set; }

        /// <summary>
        /// Ports exposed by the container
        /// </summary>
        public int[] Ports { get; set; }

        /// <summary>
        /// Container readiness
        /// </summary>
        public bool Ready { get; set; }

        /// <summary>
        /// Container state
        /// </summary>
        public ContainerState State { get; set; }

        /// <summary>
        /// Number of times container has restarted
        /// </summary>
        public int RestartCount { get; set; }

        /// <summary>
        /// Indicates whether a build container is running
        /// </summary>
        public bool IsBuildContainerRunning { get; set; }
    }
}