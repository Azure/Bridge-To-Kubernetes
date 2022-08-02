// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// A block of data streamed over a forwarded port
    /// Please don't make this class internal as some external library depends
    /// on this class for serializing and de-serializing
    /// </summary>
    public class PortForwardStreamBlock
    {
        /// <summary>
        /// Unique identifier for the stream
        /// </summary>
        public int StreamId { get; set; }

        /// <summary>
        /// Stream message type
        /// </summary>
        public PortForwardStreamFlag Flag { get; set; }

        /// <summary>
        /// Streamed content
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        /// Create the first <see cref="PortForwardStreamBlock"/> from a newly-connected stream
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        public static PortForwardStreamBlock Connected(int streamId)
        {
            return new PortForwardStreamBlock()
            {
                StreamId = streamId,
                Flag = PortForwardStreamFlag.Connected
            };
        }

        /// <summary>
        /// Create the last <see cref="PortForwardStreamBlock"/> from a stream that has been closed
        /// </summary>
        /// <param name="streamId"></param>
        /// <returns></returns>
        public static PortForwardStreamBlock Closed(int streamId)
        {
            return new PortForwardStreamBlock()
            {
                StreamId = streamId,
                Flag = PortForwardStreamFlag.Closed
            };
        }

        /// <summary>
        /// Create a <see cref="PortForwardStreamBlock"/> containing a block of data read from an open stream
        /// </summary>
        /// <param name="streamId"></param>
        /// <param name="data"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static PortForwardStreamBlock Data(int streamId, byte[] data, int length)
        {
            var block = new PortForwardStreamBlock()
            {
                StreamId = streamId,
                Flag = PortForwardStreamFlag.Data,
                Content = new byte[length]
            };
            Array.Copy(data, block.Content, length);
            return block;
        }
    }
}