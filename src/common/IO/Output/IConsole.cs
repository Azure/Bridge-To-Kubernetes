// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal interface IConsole
    {
        /// <summary>
        /// Writes the specified string value to the standard output stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        void Write(string value);

        /// <summary>
        /// Writes the specified string value, followed by the current line terminator, to the standard output stream
        /// </summary>
        /// <param name="value">The value to write</param>
        void WriteLine(string value);

        /// <summary>
        /// Writes the specified string value to the standard error output stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        void WriteError(string value);

        /// <summary>
        /// Clears all buffers for the standard output writer and causes any buffered data to be
        ///  written to the underlying device
        /// </summary>
        void FlushOutput();

        /// <summary>
        /// Clears all buffers for the standard error writer and causes any buffered data to be
        /// written to the underlying device
        /// </summary>
        void FlushError();
    }
}