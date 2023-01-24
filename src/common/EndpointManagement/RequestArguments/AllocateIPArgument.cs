// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// 
    /// </summary>
    public class AllocateIPArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<EndpointInfo> Endpoints { get; set; }
    }
}
