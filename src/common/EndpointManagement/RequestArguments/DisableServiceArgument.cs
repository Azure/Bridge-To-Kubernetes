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
    public class DisableServiceArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// 
        /// </summary>
        public IEnumerable<ServicePortMapping> ServicePortMappings { get; set; }
    }
}
