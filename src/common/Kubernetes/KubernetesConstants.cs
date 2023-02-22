// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    internal static class KubernetesConstants
    {
        public static class Namespaces
        {
            public const string AzureSystem = "azure-system";
            public const string Default = "default";
            public const string KubeNodeLease = "kube-node-lease";
            public const string KubePublic = "kube-public";
            public const string KubeSystem = "kube-system";

            // Namespaces reserved for Kubernetes and AKS
            public static readonly HashSet<string> System = new HashSet<string>
            {
                AzureSystem,
                KubeNodeLease,
                KubePublic,
                KubeSystem
            };
        }

        public static class Labels
        {
            public const string AadPodBinding = "aadpodidbinding";
            public const string Beta_OS = "beta.kubernetes.io/os";
            public const string OS = "kubernetes.io/os";

            internal static class Values
            {
                internal const string Windows = "windows";
                internal const string Linux = "linux";
            }

        }

        public static class ServiceNames
        {
            public static readonly (string serviceName, string namespaceName)[] System = { ("kubernetes", Namespaces.Default) };
        }

        public static class Limits
        {
            /// <summary>
            /// The max length of a kubernetes resource's name (Secret, Job, Pod, etc) is 253 characters:
            /// https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#dns-subdomain-names
            /// But the resource names can be added as labels so to avoid issues with label names setting 
            /// the max length for a resouce name to be max label length value.
            /// </summary>
            public const int MaxResourceNameLength = MaxLabelValueLength;

            /// <summary>
            /// The max length of a label value on a kubernetes resource
            /// https://kubernetes.io/docs/concepts/overview/working-with-objects/names/#dns-label-names
            /// </summary>
            public const int MaxLabelValueLength = 63;
        }

        public static class TypeStrings
        {
            public const string LoadBalancer = "LoadBalancer";
        }

        public static class Protocols
        {
            public const string Tcp = "tcp";
        }
    }
}