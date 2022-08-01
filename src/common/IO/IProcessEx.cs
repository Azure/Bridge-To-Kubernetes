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
    internal interface IProcessEx : IDisposable
    {
        /// <summary>
        /// <see cref="Process.StartInfo"/>
        /// </summary>
        ProcessStartInfo StartInfo { get; }

        /// <summary>
        /// <see cref="Process.ExitCode"/>
        /// </summary>
        int ExitCode { get; }

        /// <summary>
        /// <see cref="Process.Id"/>
        /// </summary>
        int Id { get; }

        /// <summary>
        /// <see cref="Process.HasExited"/>
        /// </summary>
        bool HasExited { get; }

        /// <summary>
        /// <see cref="Process.OutputDataReceived"/>
        /// </summary>
        event DataReceivedEventHandler OutputDataReceived;

        /// <summary>
        /// <see cref="Process.ErrorDataReceived"/>
        /// </summary>
        event DataReceivedEventHandler ErrorDataReceived;

        /// <summary>
        /// <see cref="Process.StandardInput"/>
        /// </summary>
        StreamWriter StandardInput { get; }

        /// <summary>
        /// <see cref="Process.StandardOutput"/>
        /// </summary>
        StreamReader StandardOutput { get; }

        /// <summary>
        /// <see cref="Process.StandardError"/>
        /// </summary>
        StreamReader StandardError { get; }

        /// <summary>
        /// <see cref="Process.Start()"/>
        /// </summary>
        bool Start();

        /// <summary>
        /// <see cref="Process.Kill()"/>
        /// </summary>
        void Kill();

        /// <summary>
        /// <see cref="Process.BeginOutputReadLine"/>
        /// </summary>
        void BeginOutputReadLine();

        /// <summary>
        /// <see cref="Process.BeginErrorReadLine"/>
        /// </summary>
        void BeginErrorReadLine();

        /// <summary>
        /// <see cref="Process.WaitForExit()"/>
        /// </summary>
        void WaitForExit();

        /// <summary>
        /// <see cref="Process.WaitForExit(int)"/>
        /// </summary>
        bool WaitForExit(int milliseconds);
    }
}