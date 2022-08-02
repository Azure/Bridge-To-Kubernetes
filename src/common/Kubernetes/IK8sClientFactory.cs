// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using k8s;
using k8s.KubeConfigModels;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    internal interface IK8sClientFactory
    {
        /// <summary>
        /// Creates a kubernetes client using the in-cluster configuration.
        /// </summary>
        IKubernetes CreateFromInClusterConfig();

        /// <summary>
        /// Creates a Kubernetes client from a kubeconfig file.
        /// This is the factory method used for the <c>IKubernetes</c> implementation.
        /// </summary>
        IKubernetes CreateFromKubeConfigFile(string kubeConfigFilePath);

        /// <summary>
        /// Creates a Kubernetes client from a kubeconfig stream.
        /// </summary>
        IKubernetes CreateFromKubeConfigStream(Stream kubeConfigStream);

        /// <summary>
        /// Creates a Kubernetes client from a kubeconfig configuration object.
        /// </summary>
        IKubernetes CreateFromKubeConfig(K8SConfiguration kubeConfig);

        /// <summary>
        /// Loads a kubeconfig configuration object from a kubeconfig stream.
        /// </summary>
        K8SConfiguration LoadKubeConfig(Stream kubeConfigStream);

        /// <summary>
        /// Loads a kubeconfig configuration object from a FileInfo.
        /// </summary>
        K8SConfiguration LoadKubeConfig(FileInfo kubeConfigFile);

        /// <summary>
        /// Loads a kubeconfig configuration object from the default or explicit file path
        /// </summary>
        K8SConfiguration LoadKubeConfig(string kubeConfigPath = null);
    }
}