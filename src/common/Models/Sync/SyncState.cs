// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// Describes the state of a project's synchronized files at a point in time
    /// </summary>
    public class SyncState
    {
        /// <summary>
        /// Name of the workspace
        /// </summary>
        public string WorkspaceName { get; set; }

        /// <summary>
        /// Time of synchronization
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// Collection of files synchronized
        /// </summary>
        public FileItem[] Files { get; set; }

        /// <summary>
        /// Quantity of space available for synchronizing files
        /// </summary>
        public long AvailableSpace { get; set; } = -1;
    }
}