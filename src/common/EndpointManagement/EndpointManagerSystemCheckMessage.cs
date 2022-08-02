// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager
{
    /// <summary>
    /// Information for the user about issues that that could interfere with their endpoints
    /// </summary>
    public class EndpointManagerSystemCheckMessage
    {
        /// <summary>
        /// Messages about bad services that globally binds to known port.
        /// </summary>
        public SystemServiceCheckMessage[] ServiceMessages { get; set; }

        /// <summary>
        /// The current port binding on the system - which process uses which port globally.
        /// </summary>
        public IDictionary<int, string> PortBinding { get; set; }
    }
}