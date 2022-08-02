// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Kubernetes Service
    /// </summary>
    public class Service
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.Kubernetes.Service"/> class.
        /// </summary>
        public Service(string name, IDictionary<string, string> selectors, IEnumerable<Endpoint> endpoints)
        {
            Name = name;
            Selectors = selectors;
            Endpoints = endpoints;
        }

        /// <summary>
        /// Gets or sets the Service name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets the service selectors
        /// </summary>
        public IDictionary<string, string> Selectors { get; }

        /// <summary>
        /// Gets or sets the endpoints
        /// </summary>
        public IEnumerable<Endpoint> Endpoints { get; }
    }
}