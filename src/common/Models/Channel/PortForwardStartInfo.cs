// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// <see cref="PortForwardStartInfo"/> describes how a port forward should be started.
    /// </summary>
    public class PortForwardStartInfo
    {
        /// <summary>
        /// Optionally specify local port number
        /// </summary>
        public int? LocalPort { get; set; }

        /// <summary>
        /// The port number.
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// HTTP status code 200 will be returned to probes listed here.
        /// </summary>
        public string[] HttpProbes { get; set; }
    }
}