// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace k8s.Models
{
    internal static class V1ContainerExtensions
    {
        public static bool IsKnownSidecarContainer(this V1Container container)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(container.Name, "devspaces-proxy")
                    || StringComparer.OrdinalIgnoreCase.Equals(container.Name, "istio-proxy")
                    || StringComparer.OrdinalIgnoreCase.Equals(container.Name, "daprd")
                    || StringComparer.OrdinalIgnoreCase.Equals(container.Name, "jaeger-agent")
                    || StringComparer.OrdinalIgnoreCase.Equals(container.Name, "linkerd-proxy");
        }
    }
}
