// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// Flag indicating the type of stream transmission
    /// </summary>
    public enum PortForwardStreamFlag
    {
        /// <summary>
        /// Connected
        /// </summary>
        Connected = 1,

        /// <summary>
        /// Data transmission
        /// </summary>
        Data = 2,

        /// <summary>
        /// Closed
        /// </summary>
        Closed = 3
    }
}