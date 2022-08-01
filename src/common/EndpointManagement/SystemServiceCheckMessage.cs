// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager
{
    /// <summary>
    /// Information for the user about system a configuration issue that could impact their experience
    /// </summary>
    public class SystemServiceCheckMessage
    {
        /// <summary>
        /// Port number this issue could impact.
        /// </summary>
        public int[] Ports { get; set; }

        /// <summary>
        /// Error or warning message associated with this port.
        /// </summary>
        public string Message { get; set; }
    }
}