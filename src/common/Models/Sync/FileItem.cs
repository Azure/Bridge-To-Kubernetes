// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// Describes a file to be synchronized
    /// </summary>
    public class FileItem
    {
        /// <summary>
        /// File path
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Time synchronized
        /// </summary>
        public long Timestamp { get; set; }

        /// <summary>
        /// File synchronization mode
        /// </summary>
        public int Mode { get; set; }
    }
}