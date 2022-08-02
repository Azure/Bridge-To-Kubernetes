// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Indicates the state of a container
    /// </summary>
    public enum ContainerState
    {
        /// <summary>
        /// State could not be determined
        /// </summary>
        Unknown,

        /// <summary>
        /// Currently running
        /// </summary>
        Running,

        /// <summary>
        /// Debugger attached
        /// </summary>
        Debugging,

        /// <summary>
        /// Waiting to run
        /// </summary>
        Waiting,

        /// <summary>
        /// Terminated
        /// </summary>
        Terminated
    }
}