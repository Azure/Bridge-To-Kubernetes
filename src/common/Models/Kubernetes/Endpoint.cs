// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Kubernetes Service Endpoint
    /// </summary>
    public class Endpoint
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.Kubernetes.Endpoint"/> class.
        /// </summary>
        public Endpoint(int portNumber, string portName, string protocol, string targetPort)
        {
            PortNumber = portNumber;
            PortName = portName;
            Protocol = protocol;
            TargetPort = targetPort;
        }

        /// <summary>
        /// Gets or sets Service endpoint's port number
        /// </summary>
        public int PortNumber { get; }

        /// <summary>
        /// Gets or sets Service endpoint's port name
        /// </summary>
        public string PortName { get; }

        /// <summary>
        /// Gets or sets Service endpoint's protocol
        /// </summary>
        public string Protocol { get; }

        /// <summary>
        /// Gets or sets Service endpoint's target port
        /// </summary>
        public string TargetPort { get; }
    }
}