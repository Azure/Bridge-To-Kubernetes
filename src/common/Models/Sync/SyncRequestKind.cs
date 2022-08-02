// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// Kind of file synchronization request
    /// </summary>
    public enum SyncRequestKind
    {
        /// <summary>
        /// No sync
        /// </summary>
        None = 0,

        /// <summary>
        /// Upload request must be followed by an UpdateMode at the end
        /// </summary>
        Upload = 1,

        /// <summary>
        /// Follows an <see cref="Upload"/> request
        /// </summary>
        UpdateMode = 2,

        /// <summary>
        /// Delete a file
        /// </summary>
        DeleteFile = 3,

        /// <summary>
        /// Delete a diretory
        /// </summary>
        DeleteDirectory = 4,

        /// <summary>
        /// Marks the end of a synchronization operation
        /// </summary>
        SyncEnd = 10
    }
}