// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// Argument to tell EndpointManager what services to disable on the local computer
    /// </summary>
    public class DisableServiceArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// Service occupying a port
        /// </summary>
        public IEnumerable<ServicePortMapping> ServicePortMappings { get; set; }
    }
}
