// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    internal interface IKubectlImpl
    {
        /// <summary>
        /// Run a short running kubectl command as a process
        /// </summary>
        /// <returns>Exit code from the kubectl process</returns>
        int RunShortRunningCommand(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut,
            Action<string> onStdErr,
            CancellationToken cancellationToken,
            Dictionary<string, string> envVariables = null,
            bool log137ExitCodeErrorAsWarning = false,
            int timeoutMs = 30000);

        /// <summary>
        /// Run a long running kubectl command as a process
        /// </summary>
        /// <returns>Exit code from the kubectl process</returns>
        int RunLongRunningCommand(
            KubernetesCommandName commandName,
            string command,
            Action<string> onStdOut,
            Action<string> onStdErr,
            CancellationToken cancellationToken,
            Dictionary<string, string> envVariables = null,
            bool log137ExitCodeErrorAsWarning = false);
    }
}