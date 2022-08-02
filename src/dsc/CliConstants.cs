// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe
{
    internal static class CliConstants
    {
        internal static string CommandFullName = $"{Product.Name} CLI";

        internal const string BridgeReportLink = "https://aka.ms/bridge-to-k8s-report";

        internal static class Dependency
        {
            public const string ServiceRunPortForward = "Service Run - Port Forward";
            public const string PrepConnect = "Prep Connect";
            public const string CleanConnect = "Clean Local Connect";
            public const string RoutingHeader = "Get Routing Header";
            public const string RoutingSupported = "Is Routing Supported";
            public const string ListIngress = "List Ingresses";
            public const string ListNamespace = "List Namespaces";
            public const string ListService = "List Services";
            public const string ListContext = "List Contexts";
        }

        internal static class Properties
        {
            public const string TargetContainerName = "TargetContainerName";
            public const string TargetDeploymentName = "TargetDeploymentName";
            public const string TargetNamespaceName = "TargetNamespaceName";
            public const string TargetPodName = "TargetPodName";
            public const string TargetServiceName = "TargetServiceName";
            public const string TargetKubeConfigContextName = "TargetKubeConfigContextName";
        }
    }
}