// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.IO
{
    /// <summary>
    /// IPlatform.
    /// </summary>
    internal interface IPlatform
    {
        [SupportedOSPlatformGuard("Windows")]
        bool IsWindows { get; }

        [SupportedOSPlatformGuard("OSX")]
        bool IsOSX { get; }

        [SupportedOSPlatformGuard("Linux")]
        bool IsLinux { get; }

        /// <summary>
        /// Determine the username of the current user
        /// </summary>
        /// <returns>The exitcode and the userName</returns>
        Task<(int exitCode, string userName)> DetermineCurrentUserWithRetriesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Execute on IPlatform with combined output
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="command"></param>
        /// <param name="logCallback">Can be null</param>
        /// <param name="envVariables"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="output"></param>
        int Execute(string executable, string command, Action<string> logCallback, IDictionary<string, string> envVariables, TimeSpan timeout, CancellationToken cancellationToken, out string output);

        /// <summary>
        /// Execute on IPlatform with stdout and stderr separated
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="command"></param>
        /// <param name="stdOutCallback">Can be null</param>
        /// <param name="stdErrCallback">Can be null</param>
        /// <param name="envVariables"></param>
        /// <param name="timeout"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="stdOutOutput"></param>
        /// <param name="stdErrOutput"></param>
        int Execute(string executable, string command, Action<string> stdOutCallback, Action<string> stdErrCallback, IDictionary<string, string> envVariables, TimeSpan timeout, CancellationToken cancellationToken, out string stdOutOutput, out string stdErrOutput);

        /// <summary>
        /// Execute on IPlatform and return exit code and output
        /// </summary>
        /// <param name="command"></param>
        /// <param name="arguments"></param>
        /// <param name="timeout"></param>
        /// <param name="stdOutCallback">Can be null</param>
        /// <param name="stdErrCallback">Can be null</param>
        /// <param name="workingDirectory"></param>
        /// <param name="processInput">The input to write to the process stdin</param>
        /// <returns></returns>
        (int exitCode, string output) ExecuteAndReturnOutput(string command, string arguments, TimeSpan timeout, Action<string> stdOutCallback, Action<string> stdErrCallback, string workingDirectory = null, string processInput = null);

        /// <summary>
        /// Creates a process object
        /// </summary>
        IProcessEx CreateProcess(ProcessStartInfo psi);

        /// <summary>
        /// Kills the process identified by <paramref name="processId"/>
        /// </summary>
        void KillProcess(int processId);
    }
}