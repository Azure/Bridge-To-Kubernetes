// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Enum indicating the type of container to connect to
    /// </summary>
    public enum ContainerIdentifierOption
    {
        /// <summary>
        /// Work with a container in the specified pod
        /// </summary>
        Pod,

        /// <summary>
        /// Work with a container in the specified Deployment template
        /// </summary>
        Deployment,

        /// <summary>
        /// Create container in a new pod
        /// </summary>
        NewPod
    }
}