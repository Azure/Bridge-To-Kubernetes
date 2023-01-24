// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using System;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.Settings
{
    /// <summary>
    /// Represents a pair of ports (local and remote) to map
    /// </summary>
    public class PortPair : ICloneable
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="localPort"></param>
        /// <param name="remotePort"></param>
        /// <param name="protocol"></param>
        [JsonConstructor]
        public PortPair(int localPort, int remotePort, string protocol = KubernetesConstants.Protocols.Tcp)
        {
            this.LocalPort = localPort;
            this.RemotePort = remotePort;
            this.Protocol = protocol;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="remotePort"></param>
        /// <param name="protocol"></param>
        public PortPair(int remotePort, string protocol = KubernetesConstants.Protocols.Tcp)
        {
            this.LocalPort = Constants.IP.PortPlaceHolder;
            this.RemotePort = remotePort;
            this.Protocol = protocol;
        }

        /// <summary>
        /// Local port corresponding to the remote port for tunneling
        /// </summary>
        [JsonPropertyName("localPort")]
        public int LocalPort { get; set; }

        /// <summary>
        /// Remote port corresponding to the local port for tunneling
        /// </summary>
        [JsonPropertyName("remotePort")]
        public int RemotePort { get; set; }

        /// <summary>
        /// Procol used when comunicating on this port (usually TCP)
        /// </summary>
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; }

        /// <summary>
        /// Create a clone of this object
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            return new PortPair(LocalPort, RemotePort, Protocol);
        }
    }
}