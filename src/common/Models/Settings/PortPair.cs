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
        /// <param name="name"></param>
        [JsonConstructor]
        public PortPair(int localPort, int remotePort, string protocol = KubernetesConstants.Protocols.Tcp, string name = null)
        {
            LocalPort = localPort;
            RemotePort = remotePort;
            Protocol = protocol;
            Name = name;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="remotePort"></param>
        /// <param name="protocol"></param>
        /// <param name="name"></param>
        public PortPair(int remotePort, string protocol = KubernetesConstants.Protocols.Tcp, string name = null)
        {
            LocalPort = Constants.IP.PortPlaceHolder;
            RemotePort = remotePort;
            Protocol = protocol;
            Name = name;
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
        /// Protocol used when communicating on this port (usually TCP)
        /// </summary>
        [JsonPropertyName("protocol")]
        public string Protocol { get; set; }

        /// <summary>
        /// Name metadata corresponding to named ports in service resource in Kubernetes
        /// </summary>
        /// <remarks>
        /// Service resources with multiple ports must be given names so that they are unambiguous.
        /// https://kubernetes.io/docs/concepts/services-networking/service/#multi-port-services
        /// </remarks>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Create a clone of this object
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            return new PortPair(LocalPort, RemotePort, Protocol, Name);
        }
    }
}
