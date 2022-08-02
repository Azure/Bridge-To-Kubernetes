// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Sync
{
    /// <summary>
    /// A block of file content to synchronize
    /// </summary>
    public class SyncRequestBlock
    {
        /// <summary>
        /// Identifier for the component requesting the synchronization
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// <see cref="SyncRequestKind"/> for this block
        /// </summary>
        public SyncRequestKind RequestKind { get; set; }

        /// <summary>
        /// The <see cref="FileItem"/> this block of content belongs in
        /// </summary>
        public FileItem FileItem { get; set; }

        /// <summary>
        /// The content to synchronize
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        /// Type of compression used. 0 for uncompressed, 1 for gzipped
        /// </summary>
        public int Compression { get; set; }
    }
}