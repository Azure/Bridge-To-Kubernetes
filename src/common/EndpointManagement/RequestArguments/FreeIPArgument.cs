// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// 
    /// </summary>
    public class FreeIPArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// 
        /// </summary>
        public IPAddress[] IPAddresses { get; set; }
    }
}
