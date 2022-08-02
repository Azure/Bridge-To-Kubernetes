// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// Type of response from a file synchronization operation
    /// </summary>
    public enum SyncResponseKind
    {
        /// <summary>
        /// Completed successfully
        /// </summary>
        Success = 0,

        /// <summary>
        /// Unexpected client Id (multiple client try to sync at the same time?)
        /// </summary>
        WrongClient = 11,

        /// <summary>
        /// Insufficient space on the server
        /// </summary>
        InSufficientSpace = 12,

        /// <summary>
        /// Generic failure
        /// </summary>
        Failure = 100
    }
}