// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// Argument to tell EndpointManager what IP addresses to allocate
    /// </summary>
    public class AllocateIPArgument : EndpointManagerRequestArgument
    {
    /// <summary>
    /// Describes service in the cluster, or any endpoint outside of the cluster, which the local process needs to communicate with via the devhostagent
    /// </summary>
    public IEnumerable<EndpointInfo> Endpoints { get; set; }
    }
}
