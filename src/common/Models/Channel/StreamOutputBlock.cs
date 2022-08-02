// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// StreamOutputBlock describes a data block sent during command execution.
    /// Please don't make this class internal as some external library depends
    /// on this class for serializing and de-serializing
    /// </summary>
    public class StreamOutputBlock
    {
        /// <summary>
        /// Output channel
        /// -1 denotes the end of program
        /// </summary>
        public int Channel { get; set; }

        /// <summary>
        /// Data to output to the stream
        /// </summary>
        public byte[] Content { get; set; }

        /// <summary>
        /// StdOut
        /// </summary>
        public const int Channel_StdOut = 0;

        /// <summary>
        /// StdErr
        /// </summary>
        public const int Channel_StdErr = 1;

        /// <summary>
        /// Exit code
        /// </summary>
        public const int Channel_ExitCode = 2;

        /// <summary>
        /// Inform execution ID so client can send StdIn requests
        /// </summary>
        public const int Channel_ExecutionId = 99;

        /// <summary>
        /// Execution error
        /// </summary>
        public const int Channel_ExecutionError = 100;
    }
}