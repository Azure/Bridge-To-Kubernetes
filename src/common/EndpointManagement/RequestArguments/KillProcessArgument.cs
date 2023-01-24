// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// 
    /// </summary>
    public class KillProcessArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<ProcessPortMapping> ProcessPortMappings { get; set; }
    }
}
