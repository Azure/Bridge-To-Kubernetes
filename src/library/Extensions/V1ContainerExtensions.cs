// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace k8s.Models
{
    internal static class V1ContainerExtensions
    {
        public static bool IsKnownSidecarContainer(this V1Container container)
        {
            var excludedContainerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                "istio-proxy",
                "linkerd-proxy",
                "devspaces-proxy",
                "nginx-proxy",
                "jaeger-agent",
                "daprd"
            };
            return excludedContainerNames.Contains(container.Name);
        }
    }
}
