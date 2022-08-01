// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using k8s;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Configuration
{
    /// <summary>
    /// <see cref="IRoutingManagerConfig" />
    /// </summary>
    internal class RoutingManagerConfig : IRoutingManagerConfig
    {
        private string _namespaceName;

        /// <summary>
        /// <see cref="IRoutingManagerConfig.GetNamespace"/>
        /// </summary>
        public string GetNamespace()
        {
            if (string.IsNullOrWhiteSpace(_namespaceName))
            {
                _namespaceName = Environment.GetEnvironmentVariable("NAMESPACE");
                if (string.IsNullOrWhiteSpace(_namespaceName))
                {
                    throw new ArgumentNullException(nameof(_namespaceName));
                }
            }

            return _namespaceName;
        }
    }
}