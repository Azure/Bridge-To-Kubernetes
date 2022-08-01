// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// File synchronization response
    /// </summary>
    public class SyncResponse
    {
        /// <summary>
        /// <see cref="SyncResponseKind"/> of the file synchronization operation
        /// </summary>
        public SyncResponseKind ResponseKind { get; set; }

        /// <summary>
        /// <see cref="SyncState"/> of the file synchronization operation
        /// </summary>
        public SyncState SyncState { get; set; }

        /// <summary>
        /// Message returned by file synchronization operatoin
        /// </summary>
        public string Message { get; set; }
    }
}