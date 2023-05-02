// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.BridgeToKubernetes.LocalAgent
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
        /// Check if a port is available on the specified IPAddress
        /// </summary>
        /// <param name="localAddress"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        bool IsLocalPortAvailable(IPAddress localAddress, int port);
    }
}