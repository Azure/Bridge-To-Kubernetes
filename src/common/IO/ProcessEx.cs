// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    /// <summary>
    /// An internal wrapper of <see cref="Process"/>. For unit testing purposes.
    /// </summary>
    internal class ProcessEx : IProcessEx
    {
        private readonly Process _process = new Process();

        /// <summary>
        /// Constructor
        /// </summary>
        public ProcessEx(ProcessStartInfo psi)
        {
            _process.StartInfo = psi ?? throw new ArgumentNullException(nameof(psi));
            _process.OutputDataReceived += (o, e) => this.OutputDataReceived?.Invoke(o, e);
            _process.ErrorDataReceived += (o, e) => this.ErrorDataReceived?.Invoke(o, e);
        }

        public event DataReceivedEventHandler OutputDataReceived;

        public event DataReceivedEventHandler ErrorDataReceived;

        public ProcessStartInfo StartInfo => _process.StartInfo;

        public int ExitCode => _process.ExitCode;

        public int Id => _process.Id;

        public bool HasExited => _process.HasExited;

        public StreamWriter StandardInput => _process.StandardInput;

        public StreamReader StandardOutput => _process.StandardOutput;

        public StreamReader StandardError => _process.StandardError;

        public void BeginErrorReadLine()
            => _process.BeginErrorReadLine();

        public void BeginOutputReadLine()
            => _process.BeginOutputReadLine();

        public bool Start()
            => _process.Start();

        public void Kill()
            => _process.Kill();

        public void WaitForExit()
            => _process.WaitForExit();

        public bool WaitForExit(int milliseconds)
            => _process.WaitForExit(milliseconds);

        public void Dispose()
            => _process.Dispose();
    }
}