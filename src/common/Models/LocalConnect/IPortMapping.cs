// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Describes a process or service running on the local machine and the port on which it is operating
    /// </summary>
    public interface IPortMapping
    {
        /// <summary>
        /// Port number
        /// </summary>
        int PortNumber { get; }

        /// <summary>
        /// Process ID
        /// </summary>
        int ProcessId { get; }
    }
}