// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.KubeConfigModels;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// This class specifies the kubeconfig and context to use when creating a management client
    /// </summary>
    public class KubeConfigDetails
    {
        /// <summary>
        /// The path of the kubeconfig
        /// </summary>
        /// <remarks>This is used for kubectl operation, as kubectl operation can update tokens and modify the config itself.</remarks>
        public string Path { get; private set; }

        /// <summary>
        /// The cluster specific configuration ready to be used with the KubernetesClient
        /// </summary>
        public K8SConfiguration Configuration { get; private set; }

        /// <summary>
        /// The context currently selected
        /// </summary>
        public Context CurrentContext { get; private set; }

        /// <summary>
        /// The namespace currently selected if any
        /// </summary>
        public string NamespaceName { get; private set; }

        /// <summary>
        /// The current selected cluster's FQDN
        /// </summary>
        public string ClusterFqdn { get; private set; }

        public KubeConfigDetails(string path, K8SConfiguration configuration, Context currentContext, string namespaceName, string clusterFqdn)
        {
            this.Path = path;
            this.Configuration = configuration;
            this.CurrentContext = currentContext;
            this.NamespaceName = namespaceName;
            this.ClusterFqdn = clusterFqdn;
        }
    }
}