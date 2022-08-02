// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// The response to a request for current state of a workspace's synchronized files
    /// </summary>
    public class SyncStateResponse
    {
        /// <summary>
        /// Name of the workspace
        /// </summary>
        public string WorkspaceName { get; set; }

        /// <summary>
        /// Time of synchronization
        /// </summary>
        public Int64 Timestamp { get; set; }

        /// <summary>
        /// The set of synchronized files
        /// </summary>
        public List<FileItem> Files { get; set; } = new List<FileItem>();
    }
}