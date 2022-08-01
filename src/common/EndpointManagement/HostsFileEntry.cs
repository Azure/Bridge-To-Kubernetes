// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager
{
    /// <summary>
    /// HostsFileEntry describes a line in hosts file
    /// </summary>
    public class HostsFileEntry
    {
        /// <summary>
        /// Host names
        /// </summary>
        public IList<string> Names { get; set; }

        /// <summary>
        /// IP address
        /// </summary>
        public string IP { get; set; }
    }
}