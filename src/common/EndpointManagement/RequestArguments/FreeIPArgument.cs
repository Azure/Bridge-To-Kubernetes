// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager.RequestArguments
{
    /// <summary>
    /// Argument to tell EndpointManager what IP addresses to free
    /// </summary>
    public class FreeIPArgument : EndpointManagerRequestArgument
    {
        /// <summary>
        /// Information about the IP addresses to free
        /// </summary>
        public IPAddress[] IPAddresses { get; set; }
    }
}
