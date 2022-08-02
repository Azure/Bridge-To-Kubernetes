// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// Wraps <see cref="System.ServiceProcess.ServiceController"/> to enable unit testing
    /// </summary>
    internal interface IServiceController : IDisposable
    {
        /// <summary>
        /// <see cref="System.ServiceProcess.ServiceController.Stop"/>
        /// </summary>
        void Stop();

        /// <summary>
        /// <see cref="System.ServiceProcess.ServiceController.WaitForStatus(System.ServiceProcess.ServiceControllerStatus)"/>
        /// </summary>
        void WaitForStatus(System.ServiceProcess.ServiceControllerStatus status);
    }
}