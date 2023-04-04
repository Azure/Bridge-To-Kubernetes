// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Describes the service occupying a port
    /// </summary>
    public class ServicePortMapping : IPortMapping
    {
        /// <summary>
        /// Represents a service occupying a port
        /// </summary>
        [JsonConstructor]
        public ServicePortMapping(string serviceName, int portNumber, int processId)
        {
            this.ServiceName = serviceName;
            this.PortNumber = portNumber;
            this.ProcessId = processId;
        }

        /// <summary>
        /// Name of the service
        /// </summary>
        public string ServiceName { get; }

        /// <summary>
        /// Occupied port number
        /// </summary>
        public int PortNumber { get; }

        /// <summary>
        /// Process ID
        /// </summary>
        public int ProcessId { get; }
    }
}