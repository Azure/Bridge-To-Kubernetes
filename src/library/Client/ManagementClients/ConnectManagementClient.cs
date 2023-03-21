// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.EndpointManagement;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.Library.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    internal class ConnectManagementClient : ManagementClientBase, IConnectManagementClient
    {
        private readonly ILocalEnvironmentManager _localEnvironmentManager;
        private readonly IPlatform _platform;
        private readonly IFileSystem _fileSystem;
        private readonly IWorkloadInformationProvider _userWorkloadInformationProvider;
        private readonly IRemoteEnvironmentManager _remoteEnvironmentManager;
        private readonly IPortMappingManager _portMappingManager;
        private readonly RemoteContainerConnectionDetailsResolver _remoteContainerConnectionDetailsResolver;
        private readonly EndpointManagementClient.Factory _endpointManagementClientFactory;
        private readonly LocalProcessConfig.Factory _localProcessConfigFactory;
        private readonly ManagementClientExceptionStrategy _managementClientExceptionStrategy;
        private AsyncLazy<RemoteContainerConnectionDetails> _remoteContainerConnectionDetails;
        private AsyncLazy<WorkloadInfo> _workloadInfo;
        private readonly bool _useKubernetesServiceEnvironmentVariables;
        private readonly bool _runContainerized;
        private readonly IProgress<ProgressUpdate> _progress;

        private Lazy<ILocalProcessConfig> _localProcessConfigFile;

        public delegate ConnectManagementClient Factory(RemoteContainerConnectionDetails containerConnectionDetails, bool useKubernetesServiceEnvironmentVariables, bool runContainerized, string userAgent, string correlationId);

        public ConnectManagementClient(
            RemoteContainerConnectionDetails containerConnectionDetails,
            IKubernetesClient kubernetesClient,
            string userAgent,
            string correlationId,
            KubernetesRemoteEnvironmentManager.Factory kubernetesRemoteEnvironmentManagerFactory,
            WorkloadInformationProvider.Factory userWorkloadInformationProviderFactory,
            RemoteContainerConnectionDetailsResolver.Factory remoteContainerConnectionDetailsResolverFactory,
            bool useKubernetesServiceEnvironmentVariables,
            bool runContainerized,
            LocalEnvironmentManager.Factory localEnvironmentManagerFactory,
            IPortMappingManager portMappingManager,
            IFileSystem fileSystem,
            EndpointManagementClient.Factory endpointManagementClientFactory,
            LocalProcessConfig.Factory localProcessConfigFactory,
            ManagementClientExceptionStrategy managementClientExceptionStrategy,
            IProgress<ProgressUpdate> progress,
            ILog log,
            IOperationContext operationContext,
            IPlatform platform) : base(log, operationContext)
        {
            _operationContext.UserAgent = userAgent;
            _operationContext.CorrelationId = correlationId + LoggingConstants.CorrelationIdSeparator + LoggingUtils.NewId();
            _operationContext.LoggingProperties[LoggingConstants.Property.IsRoutingEnabled] = !string.IsNullOrEmpty(containerConnectionDetails?.RoutingHeaderValue);
            _endpointManagementClientFactory = endpointManagementClientFactory;
            _localProcessConfigFactory = localProcessConfigFactory;
            _managementClientExceptionStrategy = managementClientExceptionStrategy;
            _platform = platform;
            _fileSystem = fileSystem;
            _portMappingManager = portMappingManager;
            _progress = progress;
            _remoteContainerConnectionDetailsResolver = remoteContainerConnectionDetailsResolverFactory(kubernetesClient);
            _remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(async () => await _remoteContainerConnectionDetailsResolver.ResolveConnectionDetails(containerConnectionDetails, CancellationToken.None), false);
            _userWorkloadInformationProvider = userWorkloadInformationProviderFactory(kubernetesClient, _remoteContainerConnectionDetails);
            _remoteEnvironmentManager = kubernetesRemoteEnvironmentManagerFactory(kubernetesClient, _remoteContainerConnectionDetails);
            _localEnvironmentManager = localEnvironmentManagerFactory(kubernetesClient, useKubernetesServiceEnvironmentVariables);
            _useKubernetesServiceEnvironmentVariables = useKubernetesServiceEnvironmentVariables;
            _runContainerized = runContainerized;

            // Because the parsing can throw it is better to wrap this in lazy, so it doesn't throw in the constructor. Throwing in the constructor is bad because Autofac doesn't handle this properly.
            this._localProcessConfigFile = new Lazy<ILocalProcessConfig>(() => this._ParseConfigFile(containerConnectionDetails.LocalProcessConfigFilePath));
            this._workloadInfo = new AsyncLazy<WorkloadInfo>(async () => await this._userWorkloadInformationProvider.GatherWorkloadInfo(_localProcessConfigFile.Value, CancellationToken.None));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.GetWorkloadInfo"/>
        /// </summary>
        public async Task<WorkloadInfo> GetWorkloadInfo()
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    return await _workloadInfo;
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToGetWorkloadInfo));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.GetElevationRequestsAsync"/>
        /// </summary>
        public async Task<IEnumerable<IElevationRequest>> GetElevationRequestsAsync(CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    if (this._useKubernetesServiceEnvironmentVariables)
                    {
                        return null;
                    }

                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.GetElevationRequestsAsync))
                    {
                        var environmentVariables = await this._userWorkloadInformationProvider.WorkloadEnvironment;
                        var isDapr = environmentVariables.ContainsKey("DAPR_HTTP_PORT") &&
                            environmentVariables.ContainsKey("DAPR_GRPC_PORT");

                        var result = new List<IElevationRequest>();

                        // Find reachable services for the remote target
                        // If the cluster uses Dapr, then we don't automatically load the services in the same namespace as all communication should go through Dapr.
                        // We do still load services and endpoints if they are defined in the localProcessConfig.
                        var reachableServices = await this._userWorkloadInformationProvider.GetReachableEndpointsAsync(
                            namespaceName: (await this._remoteContainerConnectionDetails).NamespaceName,
                            localProcessConfig: this._localProcessConfigFile.Value,
                            includeSameNamespaceServices: !isDapr,
                            cancellationToken: cancellationToken);

                        if (reachableServices == null || !reachableServices.Any())
                        {
                            // If there are not reachable service we don't need to map any port nor touch the host file.
                            return result;
                        }

                        reachableServices = this._portMappingManager.AddLocalPortMappings(reachableServices).ToList();

                        if (_platform.IsWindows)
                        {
                            var occupiedWindowsPorts = this._portMappingManager.GetOccupiedWindowsPortsAndPids(reachableServices);

                            foreach (var occupiedPort in occupiedWindowsPorts)
                            {
                                var port = occupiedPort.Key;
                                var processId = occupiedPort.Value;
                                if (processId == 4) // PID 4 is "System"
                                {
                                    // Check to see if known services are running
                                    var services = ServiceController.GetServices().Where(s => s.Status == ServiceControllerStatus.Running);
                                    foreach (var service in services)
                                    {
                                        if (EndpointManager.NonCriticalWindowsPortListeningServices.TryGetValue(service.DisplayName, out int[] ports)
                                            && ports.Contains(port))
                                        {
                                            IFreePortRequest disableServiceRequest = new FreePortRequest();
                                            disableServiceRequest.OccupiedPorts.Add(new ServicePortMapping(service.DisplayName, portNumber: port, processId: processId));
                                            result.Add(disableServiceRequest);
                                        }
                                    }
                                    continue;
                                }

                                Process process = null;
                                try
                                {
                                    process = Process.GetProcessById(processId);
                                }
                                catch (Exception e)
                                {
                                    // The process we are trying to look up has likely been deleted
                                    _log.ExceptionAsWarning(e);
                                    continue;
                                }

                                IFreePortRequest freePortRequest = new FreePortRequest();
                                freePortRequest.OccupiedPorts.Add(new ProcessPortMapping(process.ProcessName, portNumber: port, processId: processId));
                                result.Add(freePortRequest);
                            }
                        }

                        // If we got here is because there are ports to map, if the EndpointManager is not running, we know that the hosts file has not been modified to include the dependencies
                        // required by the service, and thus we add editing the hosts file to the list of requests.
                        var endpointManagementClient = _endpointManagementClientFactory(_operationContext.UserAgent, _operationContext.CorrelationId);
                        if (!(await endpointManagementClient.PingEndpointManagerAsync(cancellationToken)))
                        {
                            result.Add(new EditHostsFileRequest());
                        }

                        perfLogger.SetSucceeded();
                        return result;
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToGetElevationRequests));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.StartEndpointManagerAsync(CancellationToken)"/>
        /// </summary>
        public Task StartEndpointManagerAsync(CancellationToken cancellationToken)
        {
            return this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    if (this._useKubernetesServiceEnvironmentVariables)
                    {
                        return;
                    }

                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.StartEndpointManagerAsync))
                    {
                        _log.Info("Starting EndpointManager...");
                        var endpointManagementClient = _endpointManagementClientFactory(_operationContext.UserAgent, _operationContext.CorrelationId);
                        await endpointManagementClient.StartEndpointManagerAsync(cancellationToken);
                        perfLogger.SetSucceeded();
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig("Failed to start EndpointManager."));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.StartRemoteAgentAsync"/>
        /// </summary>
        public async Task<RemoteAgentInfo> StartRemoteAgentAsync(CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.StartRemoteAgentAsync))
                    {
                        var connectionDetails = await this._remoteContainerConnectionDetails;

                        // Pull the workloadInfo before deploying the remore agent. This way we don't have to worry about the agent specific environment.
                        var workloadInfo = await this.GetWorkloadInfo();

                        perfLogger.SetProperty(nameof(connectionDetails.AgentHostingMode), JsonHelpers.SerializeForLoggingPurpose(connectionDetails.AgentHostingMode));
                        perfLogger.SetProperty(nameof(connectionDetails.SourceEntityType), JsonHelpers.SerializeForLoggingPurpose(connectionDetails.SourceEntityType));
                        perfLogger.SetProperty(nameof(connectionDetails.NamespaceName), new PII(connectionDetails.NamespaceName));
                        perfLogger.SetProperty(nameof(connectionDetails.ContainerName), new PII(connectionDetails.ContainerName));
                        perfLogger.SetProperty(nameof(connectionDetails.ServiceName), new PII(connectionDetails.ServiceName));
                        perfLogger.SetProperty(nameof(connectionDetails.DeploymentName), new PII(connectionDetails.DeploymentName));
                        perfLogger.SetProperty(nameof(connectionDetails.PodName), new PII(connectionDetails.PodName));
                        perfLogger.SetProperty(nameof(connectionDetails.RoutingHeaderValue), new PII(connectionDetails.RoutingHeaderValue));
                        perfLogger.SetProperty(nameof(connectionDetails.LocalProcessConfigFilePath), new PII(connectionDetails.LocalProcessConfigFilePath));

                        if (_localProcessConfigFile.Value != null)
                        {
                            this._ReportProgress(Resources.LoadedBridgeToKubernetesEnvFileFormat, Product.Name, _localProcessConfigFile.Value.ConfigFilePath);
                        }

                        var result = await _remoteEnvironmentManager.StartRemoteAgentAsync(
                            localProcessConfig: _localProcessConfigFile.Value,
                            cancellationToken: cancellationToken);

                        perfLogger.SetSucceeded();
                        return result;
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToStartRemoteAgent));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.StartLocalAgentAsync"/>
        /// </summary>
        public async Task<string> StartLocalAgentAsync(int[] localPorts, KubeConfigDetails kubeConfigDetails, RemoteAgentInfo remoteAgentInfo, CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.StartLocalAgentAsync))
                    {
                        var workloadInfo = await this.GetWorkloadInfo();

                        var reversePortForwardInfo = workloadInfo.ReversePortForwardInfo;
                        for (int i = 0; i < localPorts.Length && i < reversePortForwardInfo.Count(); i++)
                        {
                            reversePortForwardInfo.ElementAt(i).LocalPort = localPorts[i];
                        }

                        var localAgentContainerName = _localEnvironmentManager.StartLocalAgent(workloadInfo, kubeConfigDetails, remoteAgentInfo);

                        perfLogger.SetSucceeded();
                        return localAgentContainerName;
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToStartLocalAgent));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.ConnectToRemoteAgentAsync"/>
        /// </summary>
        public async Task<int> ConnectToRemoteAgentAsync(RemoteAgentInfo remoteAgentInfo, CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.ConnectToRemoteAgent))
                    {
                        var remoteAgentLocalPort = await _remoteEnvironmentManager.ConnectToRemoteAgentAsync(remoteAgentInfo, cancellationToken);
                        perfLogger.SetSucceeded();
                        return remoteAgentLocalPort;
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToConnectToRemoteAgent));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.AddLocalMappingsAsync"/>
        /// </summary>
        public async Task AddLocalMappingsAsync(int[] localPorts, IEnumerable<IElevationRequest> elevationRequests, CancellationToken cancellationToken)
        {
            await this._managementClientExceptionStrategy.RunWithHandlingAsync(
               async () =>
               {
                   using (var perfLogger = _log.StartPerformanceLogger(
                       Events.ConnectManagementClient.AreaName,
                       Events.ConnectManagementClient.Operations.AddLocalMappings))
                   {
                       var workloadInfo = await this.GetWorkloadInfo();

                       // assign local ports to remote container ports
                       // TODO (lolodi): The user is passing local ports as an array, and then we collect the container ports from the container object in kubernetes.
                       // We then map them in the same order and we hope that we are lucky and the order is correct. There is probably a better way to do this...
                       var reversePortForwardInfo = workloadInfo.ReversePortForwardInfo;
                       for (int i = 0; i < localPorts.Length && i < reversePortForwardInfo.Count(); i++)
                       {
                           reversePortForwardInfo.ElementAt(i).LocalPort = localPorts[i];
                       }

                       // assign local ports and IPs to the remote services that need to be reached
                       if (_useKubernetesServiceEnvironmentVariables)
                       {
                           _localEnvironmentManager.AddLocalMappingsUsingClusterEnvironmentVariables(workloadInfo, cancellationToken);
                       }
                       else
                       {
                           await _localEnvironmentManager.AddLocalMappingsAsync(workloadInfo, elevationRequests, cancellationToken);
                       }
                       perfLogger.SetSucceeded();
                   }
               },
               new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToMapRemoteServices));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.StartServicePortForwardingsAsync"/>
        /// </summary>
        public async Task StartServicePortForwardingsAsync(int remoteAgentLocalPort, IEnumerable<EndpointInfo> reachableEndpoints, IEnumerable<PortForwardStartInfo> reversePortForwardInfo, CancellationToken cancellationToken)
        {
            await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.StartServicePortForwardingsAsync))
                    {
                        var devhostAgentClientTarget = this._remoteEnvironmentManager.GetRemoteAgentLocalPort();
                        _localEnvironmentManager.StartServicePortForwardings(remoteAgentLocalPort, reachableEndpoints, cancellationToken);
                        _localEnvironmentManager.StartReversePortForwarding(remoteAgentLocalPort, reversePortForwardInfo, cancellationToken);
                        perfLogger.SetSucceeded();
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToConnectToRemoteAgent));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.GetLocalEnvironment"/>
        /// </summary>
        public async Task<IDictionary<string, string>> GetLocalEnvironment(int[] workloadLocalPorts, CancellationToken cancellationToken)
        {
            return await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.ConfigureLocalHostAsync))
                    {
                        var workloadInfo = await this.GetWorkloadInfo();
                        perfLogger.SetProperty("NumOfReachableServices", workloadInfo.ReachableEndpoints.Count());
                        perfLogger.SetProperty("NumOfEnvironmentVariables", workloadInfo.EnvironmentVariables.Count());
                        perfLogger.SetProperty("NumOfContainerPorts", workloadInfo.ReversePortForwardInfo.Count());
                        perfLogger.SetProperty("NumOfVolumeMounts", workloadInfo.VolumeMounts.Count());

                        var remoteAgentLocalPort = this._remoteEnvironmentManager.GetRemoteAgentLocalPort();
                        var result = await _localEnvironmentManager.GetLocalEnvironment(remoteAgentLocalPort, workloadInfo, workloadLocalPorts, this._localProcessConfigFile.Value, cancellationToken);
                        perfLogger.SetSucceeded();
                        return result;
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToConfigureLocalHost));
        }

        // TODO: (lolodi) this should be embedded in StartRemoteAgent since this is taking care of restarting the remote agent when it stops.
        // This change should be done together with the CLI trying to borrow the logic from there.
        public Task WaitRemoteAgentChangeAsync(CancellationToken cancellationToken)
        {
            return this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.WaitRemoteAgentChangeAsync))
                    {
                        var result = await _remoteEnvironmentManager.WaitForWorkloadStoppedAsync(cancellationToken);
                        perfLogger.SetSucceeded();
                        return result;
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToWaitForRemoteAgent));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.StopLocalConnectionAsync"
        /// </summary>
        public Task StopLocalConnectionAsync(CancellationToken cancellationToken)
        {
            return this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.StopLocalConnectionAsync))
                    {
                        await _localEnvironmentManager.StopAsync(cancellationToken);
                        perfLogger.SetSucceeded();
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToStopLocalConnection));
        }

        /// <summary>
        /// <see cref="IConnectManagementClient.StopLocalAgentAsync"/>
        /// </summary>
        public async Task StopLocalAgentAsync(string localAgentContainerName, CancellationToken cancellationToken)
        {
            await this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.StopLocalAgentAsync))
                    {
                        var workloadInfo = await this.GetWorkloadInfo();
                        _localEnvironmentManager.StopLocalAgent(localAgentContainerName);

                        perfLogger.SetSucceeded();
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToStopLocalAgent));
        }

        public Task RestoreOriginalRemoteContainerAsync(CancellationToken cancellationToken)
        {
            return this._managementClientExceptionStrategy.RunWithHandlingAsync(
                async () =>
                {
                    using (var perfLogger = _log.StartPerformanceLogger(
                        Events.ConnectManagementClient.AreaName,
                        Events.ConnectManagementClient.Operations.RestoreOriginalRemoteContainerAsync))
                    {
                        await _remoteEnvironmentManager.RestoreWorkloadAsync(cancellationToken);
                        perfLogger.SetSucceeded();
                    }
                },
                new ManagementClientExceptionStrategy.FailureConfig(Resources.FailedToRestoreOriginalContainer));
        }

        #region Private methods

        private ILocalProcessConfig _ParseConfigFile(string envFilePath)
        {
            if (!string.IsNullOrEmpty(envFilePath) && _fileSystem.FileExists(envFilePath))
            {
                var localProcessConfig = _localProcessConfigFactory(envFilePath);

                foreach (var issue in localProcessConfig.AllIssues)
                {
                    if (issue.IssueType == EnvironmentEntryIssueType.Error)
                    {
                        this._log.Error(issue.Message);
                    }
                    else
                    {
                        this._log.Info(issue.Message);
                    }
                }
                if (!localProcessConfig.IsSuccess)
                {
                    string issues = string.Join(", ", localProcessConfig.ErrorIssues.Select(i => $"\"{i.Message}\""));
                    throw new UserVisibleException(_operationContext, Resources.FailedToLoadBridgeToKubernetesEnvFileFormat, Product.Name, envFilePath, issues);
                }
                return localProcessConfig;
            }
            return null;
        }

        /// <summary>
        /// Progress reporter for <see cref="ConnectManagementClient"/>
        /// </summary>
        private void _ReportProgress(string message, params object[] args)
        {
            _progress.Report(new ProgressUpdate(0, ProgressStatus.LocalConnect, new ProgressMessage(EventLevel.Informational, _log.SaferFormat(message, args))));
        }

        #endregion Private methods
    }
}