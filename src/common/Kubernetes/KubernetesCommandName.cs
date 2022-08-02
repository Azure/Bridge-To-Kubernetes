// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    internal enum KubernetesCommandName
    {
        Null, // If CommandName above is not explicitly set, this will be the default
        Apply,
        ConfigUseContext,
        Copy,
        Delete,
        PortForward,
        ListPods,
        GetContainerEnvironment,
        ListIngressRoutes,
        DeleteIngressRoute
    }
}