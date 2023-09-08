// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Library;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace k8s.Models
{
    internal static class V1PodExtensions
    {
        public static void ThrowIfRunningOnWindowsNodes(this V1Pod pod, IOperationContext context)
        {
            if (pod.Spec.NodeSelector != null &&
                ((pod.Spec.NodeSelector.TryGetValue(KubernetesConstants.Labels.OS, out string targetOS) &&
                StringComparer.OrdinalIgnoreCase.Equals(KubernetesConstants.Labels.Values.Windows, targetOS) ||
                (pod.Spec.NodeSelector.TryGetValue(KubernetesConstants.Labels.Beta_OS, out string betaTargetOS) &&
                StringComparer.OrdinalIgnoreCase.Equals(KubernetesConstants.Labels.Values.Windows, betaTargetOS)))))
            {
                throw new UserVisibleException(context, Resources.WindowsContainersNotSupportedFormat, Product.Name);
            }
        }

        public static bool IsOwnerOfKind(this V1Pod pod, params string[] kinds) =>
           pod.Metadata?.OwnerReferences?.Any(r => kinds.Any(k => StringComparer.OrdinalIgnoreCase.Equals(r.Kind, k))) ?? false;
    }
}