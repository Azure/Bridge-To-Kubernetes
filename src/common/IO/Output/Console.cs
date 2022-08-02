// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal class Console : IConsole
    {
        public void FlushError()
        {
            try
            {
                System.Console.Error.Flush();
            }
            catch (IOException)
            {
                // ignore any console output exceptions
            }
        }

        public void FlushOutput()
        {
            try
            {
                System.Console.Out.Flush();
            }
            catch (IOException)
            {
                // ignore any console output exceptions
            }
        }

        /// <summary>
        /// Writes the specified string value to the standard output stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void Write(string value)
        {
            try
            {
                System.Console.Write(value);
            }
            catch (IOException)
            {
                // ignore any console output exceptions
            }
        }

        /// <summary>
        /// Writes the specified string value, followed by the current line terminator, to the standard output stream
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteLine(string value)
        {
            try
            {
                System.Console.WriteLine(value);
            }
            catch (IOException)
            {
                // ignore any console output exceptions
            }
        }

        /// <summary>
        /// Writes the specified string value to the standard error output stream.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteError(string value)
        {
            try
            {
                System.Console.Error.Write(value);
            }
            catch (IOException)
            {
                // ignore any console output exceptions
            }
        }
    }
}