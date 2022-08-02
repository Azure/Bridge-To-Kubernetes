// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.Kubernetes.Ingress"/> class.
    /// </summary>
    public class Ingress
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.Kubernetes.Ingress"/> class.
        /// </summary>
        public Ingress(string chartName, string spaceName)
        {
            ChartName = chartName;
            SpaceName = spaceName;
            PublicUrlInfos = new List<PublicUrlInfo>();
        }

        /// <summary>
        /// Gets or sets the name of the chart.
        /// </summary>
        [JsonProperty("chartName")]
        public string ChartName { get; }

        /// <summary>
        /// Gets or sets the space
        /// </summary>
        [JsonProperty("spaceName")]
        public string SpaceName { get; }

        /// <summary>
        /// Gets or sets information of public URLs.
        /// </summary>
        [JsonProperty("publicUrlInfos")]
        public IList<PublicUrlInfo> PublicUrlInfos { get; set; }
    }
}