// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class SecurityRequirements
    {
        /// <summary>
        /// Configures common .NET security requirements for the running process
        /// </summary>
        public static void Set()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12; // SDL requirement
        }
    }
}