// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Information about the Public Url
    /// </summary>
    public class PublicUrlInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.PublicUrlInfo"/> class.
        /// </summary>
        /// <param name="servicePort">Service port.</param>
        /// <param name="publicUrl">Public URL.</param>
        public PublicUrlInfo(string servicePort, string publicUrl)
        {
            ServicePort = servicePort;
            PublicUrl = publicUrl;
        }

        /// <summary>
        /// Gets or sets the service port.
        /// </summary>
        public string ServicePort { get; }

        /// <summary>
        /// Gets or sets the public URL.
        /// </summary>
        /// <remarks>This public URL might not be immediately available, as it depends on its registration
        /// for DNS, as well as its caching in other servers and the user's client machine.</remarks>
        public string PublicUrl { get; }
    }
}