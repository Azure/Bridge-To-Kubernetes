// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// RemoteAgentInfo contains iformation about the currently deployed remote agent.
    /// </summary>
    public class RemoteAgentInfo
    {
        /// <summary>
        /// Kubernetes namespace where the remote agent runs
        /// </summary>
        public string NamespaceName { get; set; }

        /// <summary>
        /// The name of the pod running the remote agent
        /// </summary>
        public string PodName { get; set; }

        /// <summary>
        /// Name of the container running the remote agent
        /// </summary>
        public string ContainerName { get; set; }
    }
}