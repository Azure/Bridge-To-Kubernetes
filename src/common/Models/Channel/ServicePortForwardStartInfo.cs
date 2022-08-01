// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// <see cref="ServicePortForwardStartInfo"/> describes how a service port forward should be started
    /// Please don't make this class internal as some external library depends
    /// on this class for serializing and de-serializing
    /// </summary>
    public class ServicePortForwardStartInfo
    {
        /// <summary>
        /// DNS name for the service
        /// </summary>
        public string ServiceDns { get; set; }

        /// <summary>
        /// Port the service runs on
        /// </summary>
        public int ServicePort { get; set; }

        /// <summary>
        /// Local port mapped for the service
        /// </summary>
        public int? LocalPort { get; set; }

        /// <summary>
        /// Local IP address for the service
        /// </summary>
        public IPAddress IP { get; set; }
    }
}