// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Net;
using Microsoft.BridgeToKubernetes.Common.Models;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    internal interface IPortMappingManager
    {
        /// <summary>
        /// Add local ports to the portpairs of each endpoint
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns>The same endpoints but with the local port added</returns>
        IEnumerable<EndpointInfo> AddLocalPortMappings(IEnumerable<EndpointInfo> endpoints);

        /// <summary>
        /// Add local (FREE) ports to the portpairs of each endpoint
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns>The free locally mapped ports</returns>
        /// <remarks>This is usually used for the non admin scenario with env variables</remarks>
        IEnumerable<EndpointInfo> GetRemoteToFreeLocalPortMappings(IEnumerable<EndpointInfo> endpoints);

        /// <summary>
        /// Returns a list of ports (and associated PIDs) that are already in use on Windows.
        /// </summary>
        /// <param name="endpoints"></param>
        /// <returns></returns>
        IDictionary<int, int> GetOccupiedWindowsPortsAndPids(IEnumerable<EndpointInfo> endpoints);

        /// <summary>
        /// Check if a port is available on the specified IPAddress
        /// </summary>
        /// <param name="localAddress"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        bool IsLocalPortAvailable(IPAddress localAddress, int port);
    }
}