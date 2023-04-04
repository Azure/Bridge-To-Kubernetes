// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using k8s;
using k8s.Exceptions;
using k8s.KubeConfigModels;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common.Kubernetes
{
    internal class K8sClientFactory : IK8sClientFactory
    {
        private readonly ILog _log;
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentVariables _environmentVariables;

        public K8sClientFactory(ILog log, IFileSystem fileSystem, IEnvironmentVariables environmentVariables)
        {
            _log = log;
            _fileSystem = fileSystem;
            _environmentVariables = environmentVariables;
        }

        /// <summary>
        /// Builds a InCluster configuration based on enviroment variables
        /// </summary>
        public KubernetesClientConfiguration BuildInClusterConfigFromEnvironmentVariables()
        {
            // Based on https://github.com/kubernetes-client/csharp/blob/master/src/KubernetesClient/KubernetesClientConfiguration.InCluster.cs
            // when KubernetesInClusterConfigOverwrite is specified, overwrite the hard-coded in-cluster config location /var/run/secrets/kubernetes.io/serviceaccount/
            var host = _environmentVariables.KubernetesServiceHost;
            var port = _environmentVariables.KubernetesServicePort;

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(port))
            {
                throw new KubeConfigException(
                    "unable to load in-cluster configuration, KUBERNETES_SERVICE_HOST and KUBERNETES_SERVICE_PORT must be defined");
            }

            var token = this._fileSystem.ReadAllTextFromFile(Path.Combine(_environmentVariables.KubernetesInClusterConfigOverwrite, "token"));
            var rootCAFile = Path.Combine(_environmentVariables.KubernetesInClusterConfigOverwrite, "ca.crt");

            X509Certificate2Collection certCollection = new X509Certificate2Collection();
            using (var stream = _fileSystem.OpenFileForRead(rootCAFile))
            {
                certCollection.ImportFromPem(new StreamReader(stream).ReadToEnd());
            }

            return new KubernetesClientConfiguration
            {
                Host = new UriBuilder("https", host, Convert.ToInt32(port)).ToString(),
                AccessToken = token,
                SslCaCerts = certCollection
            };
        }

        /// <summary>
        /// <see cref="IK8sClientFactory.CreateFromInClusterConfig"/>
        /// </summary>
        public IKubernetes CreateFromInClusterConfig()
        {
            KubernetesClientConfiguration config;
            if (string.IsNullOrEmpty(_environmentVariables.KubernetesInClusterConfigOverwrite))
            {
                config = KubernetesClientConfiguration.InClusterConfig();
            }
            else
            {
                config = BuildInClusterConfigFromEnvironmentVariables();
            }
            return new k8s.Kubernetes(config);
        }

        /// <summary>
        /// <see cref="IK8sClientFactory.CreateFromKubeConfigFile(string)"/>
        /// </summary>
        public IKubernetes CreateFromKubeConfigFile(string kubeConfigFilePath)
        {
            KubernetesClientConfiguration config = null;
            try
            {
                config = AsyncHelpers.RunSync(() => KubernetesClientConfiguration.BuildConfigFromConfigFileAsync(kubeconfig: new FileInfo(kubeConfigFilePath)));
            }
            catch (Exception ex) when (ex is KubeConfigException || ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                _log.Info("KubernetesClientConfiguration.BuildConfigFromConfigFile throws {0}", ex.Message);
            }
            return new k8s.Kubernetes(config);
        }

        /// <summary>
        /// <see cref="IK8sClientFactory.CreateFromKubeConfigStream(Stream)"/>
        /// </summary>
        public IKubernetes CreateFromKubeConfigStream(Stream kubeConfigStream)
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigFile(kubeConfigStream);
            return new k8s.Kubernetes(config);
        }

        /// <summary>
        /// <see cref="IK8sClientFactory.CreateFromKubeConfig(K8SConfiguration)"/>
        /// </summary>
        public IKubernetes CreateFromKubeConfig(K8SConfiguration kubeConfig)
        {
            var config = KubernetesClientConfiguration.BuildConfigFromConfigObject(kubeConfig);
            return new k8s.Kubernetes(config);
        }

        /// <summary>
        /// Loads a kubeconfig configuration object from a kubeconfig stream.
        /// </summary>
        public K8SConfiguration LoadKubeConfig(Stream kubeConfigStream)
        {
            return KubernetesClientConfiguration.LoadKubeConfig(kubeConfigStream);
        }

        /// <summary>
        /// Loads a kubeconfig configuration object from a FileInfo.
        /// </summary>
        public K8SConfiguration LoadKubeConfig(FileInfo kubeConfigFile)
        {
            return KubernetesClientConfiguration.LoadKubeConfig(kubeConfigFile);
        }

        /// <summary>
        /// Loads a kubeconfig configuration object from the default or explicit file path
        /// </summary>
        public K8SConfiguration LoadKubeConfig(string kubeConfigPath = null)
        {
            return KubernetesClientConfiguration.LoadKubeConfig(kubeConfigPath);
        }
    }
}