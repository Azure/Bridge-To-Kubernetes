// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using k8s;
using k8s.KubeConfigModels;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    internal class KubeConfigManagementClient : ManagementClientBase, IKubeConfigManagementClient
    {
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly string _targetKubeConfigContext;
        private readonly Lazy<IKubectlImpl> _kubectlClient;

        private Lazy<string> _kubeConfigPath;

        private const string KUBE_DIR_NAME = ".kube";
        private const string CONFIG_FILE_NAME = "config";

        public delegate KubeConfigManagementClient Factory(string userAgent, string correlationId, string targetKubeConfigContext = null);

        /// <summary>
        /// Creates an instance of the <see cref="KubeConfigManagementClient" /> class
        /// </summary>
        public KubeConfigManagementClient(
            string userAgent,
            string correlationId,
            IOperationContext operationContext,
            ILog log,
            IFileSystem fileSystem,
            IEnvironmentVariables environmentVariables,
            Lazy<IKubectlImpl> kubectlClient,
            string targetKubeConfigContext = null) : base(log, operationContext)
        {
            this._fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            this._environmentVariables = environmentVariables ?? throw new ArgumentNullException(nameof(environmentVariables));
            this._targetKubeConfigContext = targetKubeConfigContext; //Note: if this is specified _GetConfiguration() will update the kubeconfig setting this as currentContext.
            this._kubectlClient = kubectlClient ?? throw new ArgumentNullException(nameof(kubectlClient));
            operationContext.UserAgent = userAgent;
            operationContext.CorrelationId = correlationId + LoggingConstants.CorrelationIdSeparator + LoggingUtils.NewId();

            _kubeConfigPath = new Lazy<string>(() => _GetKubeConfigPath());
        }

        public KubeConfigDetails GetKubeConfigDetails()
        {
            var config = _GetConfiguration();
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            Context currentContext = config.Contexts.Where(context => string.Equals(config.CurrentContext, context.Name)).FirstOrDefault();
            var currentClusterName = currentContext?.ContextDetails.Cluster;
            var namespaceName = !string.IsNullOrWhiteSpace(currentContext?.ContextDetails.Namespace) ? currentContext?.ContextDetails.Namespace : "default";
            var currentCluster = config.Clusters.Where(cluster => string.Equals(currentClusterName, cluster.Name)).FirstOrDefault();
            var server = currentCluster?.ClusterEndpoint.Server;

            if (string.IsNullOrWhiteSpace(currentClusterName) || string.IsNullOrWhiteSpace(server))
            {
                _log.Warning($"Invalid kubeconfig context. CurrentClusterName: '{new PII(currentClusterName)}', Server: '{new PII(server)}'");
                throw new InvalidUsageException(_log.OperationContext, CommonResources.FailedToLoadKubeConfigContext);
            }

            string fqdn = null;
            try
            {
                var serverUri = new Uri(server);
                fqdn = serverUri.Host;
            }
            catch
            {
                _log.Warning("Invalid server value in user kubeconfig");
                throw new InvalidUsageException(_log.OperationContext, CommonResources.FailedToLoadKubeConfigContext);
            }

            _LogKubernetesCloudProvider(fqdn);

            return new KubeConfigDetails(_kubeConfigPath.Value, config, currentContext, namespaceName, fqdn);
        }

        private K8SConfiguration _GetConfiguration()
        {
            try
            {
                _log.Info("Pulling kubeconfig...");
                var config = KubernetesClientConfiguration.LoadKubeConfig(_kubeConfigPath.Value);
                if (!string.IsNullOrEmpty(_targetKubeConfigContext) && !StringComparer.OrdinalIgnoreCase.Equals(config.CurrentContext, _targetKubeConfigContext))
                {
                    try
                    {
                        _log.Info("Updating kubeconfig to use {0} as current context...", new PII(_targetKubeConfigContext));

                        var exitCode = _kubectlClient.Value.RunShortRunningCommand(
                           KubernetesCommandName.ConfigUseContext,
                           $"config use-context {_targetKubeConfigContext}",
                           (string output) => { _log.Verbose("Output: {0}", output); },
                           (string error) => { _log.Warning("Error: {0}", error); },
                           CancellationToken.None);
                        if (exitCode != 0)
                        {
                            throw new UserVisibleException(this._operationContext, $"Unable to set {_targetKubeConfigContext} as current context.");
                        }
                        _log.Info("Kubeconfig updated successfully.");
                    }
                    catch (Exception ex)
                    {
                        _log.Error("Failed to set {0} as current context.", new PII(_targetKubeConfigContext));
                        _log.Exception(ex);
                        throw;
                    }
                    _log.Info("Re-pulling kubeconfig...");
                    config = KubernetesClientConfiguration.LoadKubeConfig(_kubeConfigPath.Value);
                }
                return config;
            }
            catch (Exception e) when (e is InvalidUsageException)
            {
                _log.Warning(e.Message);
                throw;
            }
            catch (Exception e)
            {
                _log.Warning("Failed to load kubeconfig at '{0}': {1}", new PII(_kubeConfigPath.Value), new PII(e.Message));
                _log.Exception(e);
                throw new InvalidKubeConfigFileException(CommonResources.FailedToLoadKubeConfigFormat, Common.Constants.Troubleshooting.FailedToLoadKubeConfigLink);
            }
        }

        private string _GetKubeConfigPath()
        {
            if (string.IsNullOrEmpty(_environmentVariables.KubeConfig))
            {
                return _fileSystem.Path.Combine(_fileSystem.GetPersistedFilesDirectory(KUBE_DIR_NAME), CONFIG_FILE_NAME);
            }
            else if (_fileSystem.Path.IsPathRooted(_environmentVariables.KubeConfig))
            {
                return _environmentVariables.KubeConfig;
            }
            else
            {
                return _fileSystem.Path.Combine(Environment.CurrentDirectory, _environmentVariables.KubeConfig);
            }
        }

        private void _LogKubernetesCloudProvider(string fqdn)
        {
            object cloud = null;
            try
            {
                var ip = IPAddress.Parse(fqdn);
                if (IPAddress.IsLoopback(ip))
                {
                    cloud = "Local IP";
                }
                else
                {
                    cloud = "Remote IP";
                }
            }
            catch (Exception)
            {
                // It's not an IP, move on, nothing to do here
            }
            if (cloud == null)
            {
                try
                {
                    var fqdnDomainParts = fqdn.Split(".");
                    var fqdnDomain = string.Join(".", fqdnDomainParts[fqdnDomainParts.Length - 2], fqdnDomainParts[fqdnDomainParts.Length - 1]);
                    switch (fqdnDomain)
                    {
                        case "azmk8s.io":
                            cloud = "AzurePublic";
                            break;

                        case "azk8s.cn":
                            cloud = "AzureChina";
                            break;

                        case "azure.us":
                            cloud = "AzureUSGovernment";
                            break;

                        case "amazonaws.com":
                            cloud = "Amazon";
                            break;

                        case "ibm.com":
                            cloud = "IBM";
                            break;

                        case "docker.internal":
                            cloud = "DockerInternal";
                            break;

                        default:
                            cloud = new PII(fqdnDomain);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning("Failed to parse fqdn domain from {0} with: {1}", new PII(fqdn), ex.Message);
                    _log.ExceptionAsWarning(ex);
                }
            }
            if (cloud != null)
            {
                _log.Event("CloudProvider", new Dictionary<string, object> { { "ClusterFQDNDomain", cloud } });
            }
        }
    }
}