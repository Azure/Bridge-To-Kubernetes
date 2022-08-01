// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Describes an operation requiring admin privileges
    /// </summary>
    public enum ElevationRequestType
    {
        /// <summary>
        /// Elevation request for editing the HOSTS file
        /// </summary>
        EditHostsFile,

        /// <summary>
        /// Elevation request for freeing a port by terminating a service or process
        /// </summary>
        FreePort
    }
}