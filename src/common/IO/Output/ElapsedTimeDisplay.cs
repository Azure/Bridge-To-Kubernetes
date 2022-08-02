// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal class ElapsedTimeDisplay : IDisposable
    {
        private Stopwatch _stopWatch;
        private IConsoleOutput _consoleOutput;

        public ElapsedTimeDisplay(IConsoleOutput consoleOutput)
        {
            this._stopWatch = Stopwatch.StartNew();
            this._consoleOutput = consoleOutput;
        }

        /// <summary>
        /// Ends the running stop watch and prints the elapsed time to the console
        /// </summary>
        public void Dispose()
        {
            if (this._stopWatch == null || !this._stopWatch.IsRunning)
            {
                return;
            }
            this._stopWatch.Stop();
            this._consoleOutput.Info($"{this._stopWatch.Elapsed.GetUIFormattedString()}", newLine: true);
        }
    }
}