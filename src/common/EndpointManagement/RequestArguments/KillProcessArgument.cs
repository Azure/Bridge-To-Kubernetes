// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// Argument to tell EndpointManager what processes to kill to free ports that are in use
    /// </summary>
    public class KillProcessArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// Process occupying ports
        /// </summary>
        public IEnumerable<ProcessPortMapping> ProcessPortMappings { get; set; }
    }
}
