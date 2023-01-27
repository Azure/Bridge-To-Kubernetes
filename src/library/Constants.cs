// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library
{
    internal static class Constants
    {
        internal static class AzureDevSpacesService
        {
            public const string UserClusterNamespaceName = "azds";
        }

        internal static class DeploymentConfig 
        {
            public const string ServiceAnnotations = "bridgetokubernetes/ignore-ports";
        }

        internal static class AzureContainerServiceResource
        {
            public const string AKSCluster = "AKS cluster";
            public const string Domain = "azmk8s.io";
        }

        internal static class PortConstants
        {
            public const string Http = "http";
            public const string Http80 = "80";
            public const string Https = "https";
            public const string Https443 = "443";
        }

        internal static class Https
        {
            public const string CertManagerAnnotationKey = "cert-manager.io/cluster-issuer";
            public const string LetsEncryptAnnotationValue = "letsencrypt";
            public const int LetsEncryptMaxDomainLength = 63;
        }

        internal static class Config
        {
            public const string FilePath = "KubernetesLocalProcessConfig.yaml";

            internal static class Tokens
            {
                public const string VolumeMounts = "volumeMounts";
                public const string Services = "services";
                public const string ExternalEndpoints = "externalEndpoints";
            }
        }
    }
}