// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// When connecting to a container we can identify it through various types of K8s entities listed here.
    /// </summary>
    internal enum KubernetesEntityType
    {
        Pod,
        Deployment,
        Service,
        StatefulSet,
        ReplicaSet,
        Unknown
    }
}