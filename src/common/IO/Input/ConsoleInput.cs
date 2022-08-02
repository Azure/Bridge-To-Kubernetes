// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;

namespace Microsoft.BridgeToKubernetes.Common.IO.Input
{
    internal class ConsoleInput : IConsoleInput
    {
        /// <summary>
        /// Reads a line from console
        /// </summary>
        /// <returns>The line entered by the user on the console</returns>
        public string ReadLine()
        {
            bool resetCursor = true;

            // Try to show the cursor
            try
            {
                Console.CursorVisible = true;
            }
            catch (Exception)
            {
                // We failed to make the cursor visible so don't try to hide it later
                resetCursor = false;
            }

            // ReadLine
            string userResponse = Console.ReadLine()?.Trim();

            // Try to hide the cursor if we made it visible earlier
            if (resetCursor)
            {
                try
                {
                    Console.CursorVisible = false;
                }
                catch { }
            }
            return userResponse;
        }

        public TextReader InputReader
        {
            get { return Console.In; }
        }
    }
}