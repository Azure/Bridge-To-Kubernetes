// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Kubernetes container
    /// </summary>
    public class Container
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.Kubernetes.Container"/> class.
        /// </summary>
        public Container(string id, string name, string image, int restartCount, ContainerState state)
        {
            Id = id;
            Name = name;
            Image = image;
            RestartCount = restartCount;
            State = state;
        }

        /// <summary>
        /// Container Id
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Container Name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Container image
        /// </summary>
        public string Image { get; }

        /// <summary>
        /// Container restart count
        /// </summary>
        public int RestartCount { get; }

        /// <summary>
        /// Container state
        /// </summary>
        public ContainerState State { get; }

        internal IDictionary<string, object> GetEventParameters()
        {
            return new Dictionary<string, object>
            {
                { nameof(Name), new PII(Name) },
                { nameof(Id), new PII(Id) },
                { nameof(RestartCount),  RestartCount },
                { nameof(State),  State }
            };
        }
    }
}