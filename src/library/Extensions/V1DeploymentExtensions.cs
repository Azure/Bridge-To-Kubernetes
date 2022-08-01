// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Library;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace k8s.Models
{
    internal static class V1DeploymentExtensions
    {
        public static void ThrowIfRunningOnWindowsNodes(this V1Deployment deployment, IOperationContext context)
        {
            if (deployment.Spec.Template.Spec.NodeSelector != null &&
                ((deployment.Spec.Template.Spec.NodeSelector.TryGetValue(KubernetesConstants.Labels.OS, out string targetOS) &&
                StringComparer.OrdinalIgnoreCase.Equals(KubernetesConstants.Labels.Values.Windows, targetOS) ||
                (deployment.Spec.Template.Spec.NodeSelector.TryGetValue(KubernetesConstants.Labels.Beta_OS, out string betaTargetOS) &&
                StringComparer.OrdinalIgnoreCase.Equals(KubernetesConstants.Labels.Values.Windows, betaTargetOS)))))
            {
                throw new UserVisibleException(context, Resources.WindowsContainersNotSupportedFormat, Product.Name);
            }
        }
    }
}