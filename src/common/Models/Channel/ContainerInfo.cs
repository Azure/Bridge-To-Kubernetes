// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// Describes a channel to a running container
    /// </summary>
    public class ContainerInfo
    {
        /// <summary>
        /// Container name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The processes running in the container
        /// </summary>
        public RunningProcessInfo[] Processes { get; set; }

        /// <summary>
        /// Unique session identifier
        /// </summary>
        public string SessionId { get; set; }

        /// <summary>
        /// The container's environment variables
        /// </summary>
        public IDictionary<string, string> EnvironmentVariables { get; set; }
    }
}