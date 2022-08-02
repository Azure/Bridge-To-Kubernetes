// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.ServiceProcess;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// <see cref="IServiceController"/>
    /// </summary>
    internal class ServiceController : IServiceController
    {
        private readonly System.ServiceProcess.ServiceController _serviceController;

        public ServiceController(string serviceName)
            => _serviceController = new System.ServiceProcess.ServiceController(serviceName);

        public void Dispose()
            => _serviceController.Dispose();

        public void Stop()
            => _serviceController.Stop();

        public void WaitForStatus(ServiceControllerStatus desiredStatus)
            => _serviceController.WaitForStatus(desiredStatus);
    }
}