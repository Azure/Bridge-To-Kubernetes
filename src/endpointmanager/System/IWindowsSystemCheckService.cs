// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.EndpointManager;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// The following items are checked:
    ///     - Windows 10: Known bad services such as BranchCache service should be stopped.
    ///     - Windows 10: Any process that binds using any-IP such as 0.0.0.0:80.
    /// </summary>
    internal interface IWindowsSystemCheckService
    {
        /// <summary>
        /// Runs the system check
        /// </summary>
        EndpointManagerSystemCheckMessage RunCheck();
    }
}