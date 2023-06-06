// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Models.Settings;
using Microsoft.BridgeToKubernetes.Common.PortForward;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.EndpointManagement;
using Microsoft.BridgeToKubernetes.Library.LocalAgentManagement;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.Library.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Implements <see cref="ILocalEnvironmentManager"/>. This implementation handles starting a workload locally without container.
    /// </summary>
    internal class LocalEnvironmentManager : ILocalEnvironmentManager
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly bool _useKubernetesServiceEnvironmentVariables;
        private readonly ILog _log;
        private readonly IPlatform _platform;
        private readonly IFileSystem _fileSystem;
        private readonly IOperationContext _operationContext;
        private readonly IPortMappingManager _portMappingManager;
        private readonly ServicePortForwardManager.Factory _servicePortForwardManagerFactory;
        private readonly ReversePortForwardManager.Factory _reversePortForwardManagerFactory;
        private readonly EndpointManagementClient.Factory _endpointManagementClientFactory;
        private readonly DevHostAgentExecutorClient.OwnedFactory _ownedDevHostAgentExecutorClientFactory;
        private readonly LocalAgentManager.Factory _localAgentManagerFactory;
        private readonly IProgress<ProgressUpdate> _progress;

        private List<IPAddress> _allocatedIPs = new List<IPAddress>();

        private Timer _endpointManagerKeepAliveTimer;
        private EndpointManagerSystemCheckMessage _systemCheckResult;
        private IServicePortForwardManager _servicePortForwardManager;
        private IReversePortForwardManager _reversePortForwardManager;

        public delegate ILocalEnvironmentManager Factory(IKubernetesClient kubernetesClient, bool useKubernetesServiceEnvironmentVariables);

        public LocalEnvironmentManager(
            IKubernetesClient kubernetesClient,
            bool useKubernetesServiceEnvironmentVariables,
            ILog log,
            IPlatform platform,
            IFileSystem fileSystem,
            IOperationContext operationContext,
            IPortMappingManager portMappingManager,
            IProgress<ProgressUpdate> progress,
            ServicePortForwardManager.Factory servicePortForwardManagerFactory,
            ReversePortForwardManager.Factory reversePortForwardManagerFactory,
            EndpointManagementClient.Factory endpointManagementClientFactory,
            DevHostAgentExecutorClient.OwnedFactory ownedDevHostAgentExecutorClientFactory,
            LocalAgentManager.Factory localAgentManagerFactory)
        {
            _kubernetesClient = kubernetesClient;
            _useKubernetesServiceEnvironmentVariables = useKubernetesServiceEnvironmentVariables;
            _log = log;
            _platform = platform;
            _fileSystem = fileSystem;
            _operationContext = operationContext;
            _portMappingManager = portMappingManager;
            _progress = progress;
            _servicePortForwardManagerFactory = servicePortForwardManagerFactory;
            _reversePortForwardManagerFactory = reversePortForwardManagerFactory;
            _endpointManagementClientFactory = endpointManagementClientFactory;
            _ownedDevHostAgentExecutorClientFactory = ownedDevHostAgentExecutorClientFactory;
            _localAgentManagerFactory = localAgentManagerFactory;
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.AddLocalMappingsAsync"/>.
        /// </summary>
        public async Task AddLocalMappingsAsync(
            WorkloadInfo workloadInfo,
            IEnumerable<IElevationRequest> elevationRequests,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.AddLocalMappings))
            {
                // Kill existing processes that might be using the required ports
                IEndpointManagementClient endpointManagementClient = _endpointManagementClientFactory(_operationContext.UserAgent, _operationContext.CorrelationId);
                if (elevationRequests != null && elevationRequests.Any())
                {
                    await endpointManagementClient.FreePortsAsync(elevationRequests, cancellationToken);
                }

                // TODO (lolodi): not sure this is actually necessary, and if it is, we should probably do it when the result of the operation is required.
                _systemCheckResult = await endpointManagementClient.SystemCheckAsync(cancellationToken);

                // Get local ports to use
                workloadInfo.ReachableEndpoints = _portMappingManager.AddLocalPortMappings(workloadInfo.ReachableEndpoints).ToList();

                // Get IP addresses (from the endpoint manager) corresponding to the local port requests starting from 127.1.1.0
                var gotIPsSuccessfully = await WebUtilities.RetryAsync(async (i) =>
                {
                    workloadInfo.ReachableEndpoints = await endpointManagementClient.AllocateIPAsync(workloadInfo.ReachableEndpoints, cancellationToken);
                    if (workloadInfo.ReachableEndpoints != null && !workloadInfo.ReachableEndpoints.Any(endpoint => endpoint.LocalIP == null))
                    {
                        return true;
                    }
                    _log.Warning("Failed to get reachable endpoints. Retrying...");
                    await Task.Delay(TimeSpan.FromMilliseconds(200));
                    return false;
                },
                numberOfAttempts: 2,
                cancellationToken: cancellationToken);

                if (!gotIPsSuccessfully)
                {
                    throw new InvalidUsageException(_operationContext, Resources.CannotObtainLocalIPsFormat, EndpointManager.ProcessName);
                }

                // Create hosts file entries for the allocated IP addresses
                _allocatedIPs.AddRange(workloadInfo.ReachableEndpoints.Select(endpointInfo => endpointInfo.LocalIP));
                List<HostsFileEntry> hostsFileEntries = new List<HostsFileEntry>();

                foreach (var endpoint in workloadInfo.ReachableEndpoints)
                {
                    endpoint.ValidateDnsName();
                    hostsFileEntries.Add(new HostsFileEntry()
                    {
                        Names = endpoint.GetServiceAliases(workloadInfo.Namespace, this._log).ToList(),
                        IP = endpoint.LocalIP.ToString(), //TODO (lolodi): HostFileEntry should use IPAddress instead of string for IP.
                    });
                }
                await endpointManagementClient.AddHostsFileEntryAsync(workloadInfo.Namespace, hostsFileEntries, cancellationToken);
                perfLogger.SetProperty("HostsFileEntryCount", hostsFileEntries.Count);
                this._ReportProgress(Resources.HostsFileUpdatedMessage);

                // Ping endpoint manager to keep it running
                this._KeepEndpointManagerAlive(endpointManagementClient);
            }
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.AddLocalMappingsUsingClusterEnvironmentVariables"/>.
        /// </summary>
        public void AddLocalMappingsUsingClusterEnvironmentVariables(
            WorkloadInfo workloadInfo,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.AddLocalMappingsUsingClusterEnvironmentVariables))
            {
                // If we are using the EnvironmentVariables (non-admin mode) we need to map all the services to 127.0.0.1 otherwise on Mac we have problems when we open tcpListeners on other IPs,
                // because we cannot run the ipconfig alias comand without being admin

                foreach (var endpointInfo in workloadInfo.ReachableEndpoints)
                {
                    endpointInfo.LocalIP = IPAddress.Loopback;
                }
                // Get local ports to use
                workloadInfo.ReachableEndpoints = this._portMappingManager.GetRemoteToFreeLocalPortMappings(workloadInfo.ReachableEndpoints).ToArray();

                if (workloadInfo.ReachableEndpoints.Any(ep => ep.LocalIP == null))
                {
                    throw new InvalidUsageException(_operationContext, Resources.CannotObtainLocalIPsFormat, EndpointManager.ProcessName);
                }
                _allocatedIPs.AddRange(workloadInfo.ReachableEndpoints.Select(endpoint => endpoint.LocalIP).ToList());
                perfLogger.SetSucceeded();
            }
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.StartServicePortForwardings"/>.
        /// </summary>
        public void StartServicePortForwardings(
            int remoteAgentLocalPort,
            IEnumerable<EndpointInfo> reachableEndpoints,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.StartServicePortForwardings))
            {
                // Stop any existing connection and create a new port forward manager
                this._servicePortForwardManager?.Stop();
                this._servicePortForwardManager = this._servicePortForwardManagerFactory(_ownedDevHostAgentExecutorClientFactory.Invoke(remoteAgentLocalPort), cancellationToken);

                foreach (var endpoint in reachableEndpoints)
                {
                    // Start TCP listener to forward requests to services in the cluster
                    this._StartServicePortForwardings(endpoint);
                }
                perfLogger.SetSucceeded();
            }
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.StartReversePortForwarding"/>
        /// </summary>
        public void StartReversePortForwarding(
            int remoteAgentLocalPort,
            IEnumerable<PortForwardStartInfo> reversePortForwardInfo,
            CancellationToken cancellationToken
            )
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.StartReversePortForwarding))
            {
                this._reversePortForwardManager?.Stop();
                this._reversePortForwardManager = this._reversePortForwardManagerFactory(_ownedDevHostAgentExecutorClientFactory.Invoke(remoteAgentLocalPort), cancellationToken);

                foreach (var pfInfo in reversePortForwardInfo)
                {
                    this._reversePortForwardManager.Start(pfInfo);
                    this._ReportProgress(Resources.ContainerPortAvailableFormat, pfInfo.Port, pfInfo.LocalPort);
                    if (pfInfo.Port == DevHostConstants.DevHostAgent.Port) {
                        _log.Warning($"Service to be debugged uses same port '{pfInfo.Port}' as Bridge DevHostAgent service does. This can cause conflict. It's recommended to update service to use different port.");
                    }
                }
                perfLogger.SetSucceeded();
            }
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.StartLocalAgent"/>
        /// </summary>
        /// TODO (lolodi): this should return a Task, we should ping the local agent to verify that it is indeed running and then complete the task
        public string StartLocalAgent(
            WorkloadInfo workloadInfo,
            KubeConfigDetails kubeConfigDetails,
            RemoteAgentInfo remoteAgentInfo)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.StartLocalAgent))
            {
                // need to assign localIPs to endpoints
                var currentIP = IPAddress.Parse(Common.Constants.IP.StartingIP).Next();
                foreach (var endpoint in workloadInfo.ReachableEndpoints)
                {
                    if (endpoint.LocalIP == null)
                    {
                        endpoint.LocalIP = currentIP;
                        currentIP = currentIP.Next();
                    }
                }

                foreach (var pfinfo in workloadInfo.ReversePortForwardInfo)
                {
                    // Because we are running containerized we don't need to remap ports
                    // The user workload will call on the expected ports, and the traffic is going to be redirected to the localAgent port by iptables rules and then forwarded
                    pfinfo.LocalPort = pfinfo.Port;
                }

                var localAgentConfig = new LocalAgentConfig();
                localAgentConfig.ReachableEndpoints = workloadInfo.ReachableEndpoints;
                localAgentConfig.ReversePortForwardInfo = workloadInfo.ReversePortForwardInfo;
                localAgentConfig.RemoteAgentInfo = remoteAgentInfo;
                var localAgentContainerName = $"{kubeConfigDetails.CurrentContext.Name}-{workloadInfo.Namespace}-{workloadInfo.WorkloadName}";
                var localAgentManager = _localAgentManagerFactory(localAgentContainerName);

                // remove containers with the same name possibly left behind by previous connections
                localAgentManager.StopLocalAgent();

                localAgentManager.StartLocalAgent(localAgentConfig, kubeConfigDetails);
                perfLogger.SetSucceeded();
                return localAgentContainerName;
            }
        }

        public void StopLocalAgent(
            string localAgentContainerName)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.StopLocalAgent))
            {
                var localAgentManager = _localAgentManagerFactory(localAgentContainerName);
                localAgentManager.StopLocalAgent();
                perfLogger.SetSucceeded();
            }
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.GetLocalEnvironment"/>
        /// </summary>
        public async Task<IDictionary<string, string>> GetLocalEnvironment(
            int remoteAgentLocalPort,
            WorkloadInfo workloadInfo,
            int[] localPorts,
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.GetLocalEnvironment))
            {
                await this._LoadAdditionalServiceEnvAsync(remoteAgentLocalPort, workloadInfo, localProcessConfig, cancellationToken);
                var result = this.CreateEnvVariablesForK8s(workloadInfo);
                perfLogger.SetSucceeded();
                perfLogger.SetProperty("EnvVarCount", result.Count);
                return result;
            }
        }

        /// <summary>
        /// <see cref="ILocalEnvironmentManager.StopAsync"/>
        /// </summary>
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.StopWorkload))
            {
                _endpointManagerKeepAliveTimer?.Dispose();
                if (_allocatedIPs.Any())
                {
                    if (!this._useKubernetesServiceEnvironmentVariables)
                    {
                        IEndpointManagementClient endpointManagementClient = _endpointManagementClientFactory(_operationContext.UserAgent, _operationContext.CorrelationId);
                        await endpointManagementClient.FreeIPAsync(_allocatedIPs.ToArray(), cancellationToken);
                    }
                    _allocatedIPs.Clear();
                }

                // Stop port forwarding
                this._servicePortForwardManager?.Stop();
                this._reversePortForwardManager?.Stop();

                perfLogger.SetSucceeded();
            }
        }

        #region Private methods

        private void _ReportProgress(string message, params object[] args)
        {
            _progress.Report(new ProgressUpdate(0, ProgressStatus.KubernetesRemoteEnvironmentManager, new ProgressMessage(EventLevel.Informational, _log.SaferFormat(message, args))));
        }

        /// <summary>
        /// Progress reporter for <see cref="LocalEnvironmentManager"/>
        /// </summary>
        private void _ReportProgress(EventLevel eventLevel, string message, params object[] args)
        {
            _progress.Report(new ProgressUpdate(0, ProgressStatus.KubernetesRemoteEnvironmentManager, new ProgressMessage(eventLevel, _log.SaferFormat(message, args))));
        }

        /// <summary>
        /// Run the service router in current process.
        /// </summary>
        private void _StartServicePortForwardings(
            EndpointInfo endpointInfo)
        {
            for (int i = 0; i < endpointInfo.Ports.Length; i++)
            {
                // TODO (lolodi): all this check should not be required since we already have mapped to ports that are not used (on linux/mac) or we did already blew up when we checked if the local port was free.
                if (_systemCheckResult != null)
                {
                    foreach (var serviceMessage in _systemCheckResult.ServiceMessages)
                    {
                        foreach (var p in serviceMessage.Ports)
                        {
                            if (p == endpointInfo.Ports[i].LocalPort)
                            {
                                _log.Info("Block StartServicePortForwardings due to bad service on port {0} {1}", p, serviceMessage.Message);
                                throw new InvalidUsageException(_operationContext, serviceMessage.Message);
                            }
                        }
                    }
                    foreach (var p in _systemCheckResult.PortBinding)
                    {
                        if (p.Key == endpointInfo.Ports[i].LocalPort)
                        {
                            var processPort = p.Key;
                            var processName = p.Value;
                            bool isSystemService = false;
                            if (processName.ToLowerInvariant().Contains("pid 4 can not obtain ownership information"))
                            {
                                processName = "PID 4 System Service";
                                isSystemService = true;
                            }

                            _log.Info("Block StartServicePortForwardings due to occupied port {0} {1}", processPort, processName);
                            if (isSystemService)
                            {
                                throw new InvalidUsageException(_operationContext, Resources.SystemProcessBindsOnPortFormat, processName, processPort, Product.Name);
                            }

                            throw new InvalidUsageException(_operationContext, Resources.ProcessBindsOnPortFormat, processName, processPort, Product.Name);
                        }
                    }
                }

                if (!this._portMappingManager.IsLocalPortAvailable(endpointInfo.LocalIP, endpointInfo.Ports[i].LocalPort))
                {
                    string additionalPortMessage = endpointInfo.Ports[i].LocalPort == 80 ? Resources.Port80WindowsServicesMessage : Resources.PortsInUseMessage;
                    throw new InvalidUsageException(_operationContext, Resources.PortInUseFormat, endpointInfo.Ports[i].LocalPort, additionalPortMessage);
                }
                // TODO (lolodi): Once we verify that we don't actually need the checks above we can just pass the whole enpoint to the _servicePortForwardManager and iterate thorugh all the ports there.
                var startInfo = new ServicePortForwardStartInfo
                {
                    ServiceDns = endpointInfo.DnsName,
                    ServicePort = endpointInfo.Ports[i].RemotePort,
                    LocalPort = endpointInfo.Ports[i].LocalPort,
                    IP = endpointInfo.LocalIP
                };

                this._servicePortForwardManager.Start(startInfo);
                this._ReportProgress(Resources.ServiceAvailableOnPortFormat, endpointInfo.DnsName, endpointInfo.LocalIP.ToString(), (this._useKubernetesServiceEnvironmentVariables || StringComparer.OrdinalIgnoreCase.Equals(endpointInfo.DnsName, DAPR)) ? endpointInfo.Ports[i].LocalPort : endpointInfo.Ports[i].RemotePort);
            }
        }

        /// <summary>
        /// Re-create the Kubernetes service related environment variables.
        /// </summary>
        public IDictionary<string, string> CreateEnvVariablesForK8s(WorkloadInfo workloadInfo)
        {
            var result = new Dictionary<string, string>(workloadInfo.EnvironmentVariables);

            foreach (var endpoint in workloadInfo.ReachableEndpoints)
            {
                if (string.Equals(endpoint.DnsName, DAPR, StringComparison.OrdinalIgnoreCase))
                {
                    // Override the DAPR env variables with the real local ports (that might be different if we neeeded to re-allocate them)
                    result["DAPR_HTTP_PORT"] = endpoint.Ports[0].LocalPort.ToString(); // TODO (lolodi): this is a hack that relies on the HTTP port to always be the first and GRPC port the second.
                    result["DAPR_GRPC_PORT"] = endpoint.Ports[1].LocalPort.ToString(); // We should probably name the port pairs (maybe with the env variable that we want to set with them).
                                                                                       // Once we do that, we can actually stop assigning the DAPR dns name ot this endpoint and just leave it empty, consistently with how the Remote agent works
                }

                // because we are using dns name instead of service we have to retrieve it by splitting when needed
                // If this ever cause issues we should consider larger refactor where we add serviceName member variable to EndpointInfo class.
                var dnsNameArray = endpoint.DnsName
                    .ToUpperInvariant()
                    .Split(".");

                var serviceName = dnsNameArray
                    .First()
                    .Replace("-", "_");

                // when !endpoint.IsInWorkloadNamespace
                var serviceNs = dnsNameArray.Length == 2
                    ? dnsNameArray
                        .Last()
                        .Replace("-", "_")
                    : null;

                // sometimes cross talk is desired between namespaces, append those not matching workload
                if (!string.IsNullOrWhiteSpace(serviceNs))
                {
                    serviceName = $"{serviceName}_{serviceNs}";
                }

                var host = _useKubernetesServiceEnvironmentVariables || string.Equals(endpoint.DnsName, DAPR, StringComparison.OrdinalIgnoreCase)
                    ? endpoint.LocalIP.ToString()
                    : endpoint.DnsName;

                if (string.Equals(serviceName, "KUBERNETES", StringComparison.OrdinalIgnoreCase))
                {
                    // reset KUBERNETES_SECRET_HOST to cluster name
                    host = _kubernetesClient.HostName;
                }

                // Service Host
                result[$"{serviceName}_SERVICE_HOST"] = host;

                // Service Port
                // tl;dr: allocate the first port in the service to the backwards-compatible environment variable in keeping with Kubernetes source code.
                // These environment variables are distinct per service definition as they do not contain metadata that distinguish them apart from other port mapping variables.
                // Kubernetes currently appears to bind the first port in the service definition to these ports, as such, we are initializing them in the outer loop here so
                // they cannot be overwritten by the final iteration of the inner loop below which would end up setting them to the final port in the collection
                var unnamedPort = _useKubernetesServiceEnvironmentVariables || string.Equals(endpoint.DnsName, DAPR, StringComparison.OrdinalIgnoreCase)
                    ? endpoint.Ports[0].LocalPort.ToString()
                    : endpoint.Ports[0].RemotePort.ToString();

                result[$"{serviceName}_SERVICE_PORT"] = unnamedPort;
                result[$"{serviceName}_PORT"] = $"{endpoint.Ports[0].Protocol}://{host}:{unnamedPort}";

                // All named ports (only the first may be unnamed according to Kubernetes source code)
                foreach (var portPair in endpoint.Ports)
                {
                    var port = _useKubernetesServiceEnvironmentVariables || string.Equals(endpoint.DnsName, DAPR, StringComparison.OrdinalIgnoreCase)
                        ? portPair.LocalPort
                        : portPair.RemotePort;

                    var protocolUpper = string.IsNullOrWhiteSpace(portPair.Protocol)
                        ? KubernetesConstants.Protocols.Tcp.ToUpperInvariant()
                        : portPair.Protocol.ToUpperInvariant();

                    result[$"{serviceName}_PORT_{port}_{protocolUpper}_PROTO"] = portPair.Protocol;
                    result[$"{serviceName}_PORT_{port}_{protocolUpper}"] = $"{portPair.Protocol}://{host}:{port}";
                    result[$"{serviceName}_PORT_{port}_{protocolUpper}_PORT"] = port.ToString();
                    result[$"{serviceName}_PORT_{port}_{protocolUpper}_ADDR"] = host;

                    if (!string.IsNullOrWhiteSpace(portPair.Name))
                    {
                        result[$"{serviceName}_SERVICE_PORT_{portPair.Name.ToUpperInvariant()}"] = port.ToString();
                    }

                    // if this is managed identity with useKubernetesServiceEnvironmentVariables set to true we have to update ms endpoint variable from dns name to ip:port
                    if (_useKubernetesServiceEnvironmentVariables && string.Equals(serviceName, ManagedIdentity.TargetServiceNameOnLocalMachine, StringComparison.OrdinalIgnoreCase))
                    {
                        result[ManagedIdentity.MSI_ENDPOINT_EnvironmentVariable] = $"http://{host}:{port}/metadata/identity/oauth2/token";
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Loads information for services specified in local proccess config
        /// </summary>
        private async Task _LoadAdditionalServiceEnvAsync(
            int remoteAgentLocalPort,
            WorkloadInfo workloadInfo,
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken)
        {
            if (localProcessConfig != null)
            {
                using (var perfLogger = _log.StartPerformanceLogger(Events.LocalEnvironmentManager.AreaName, Events.LocalEnvironmentManager.Operations.SatisfyEnvFile))
                {
                    // Download required volumes, this operation also sets the volumeToken.LocalPath to the temp directory used for the download if it was not set previously
                    foreach (var volumeToken in localProcessConfig.ReferencedVolumes)
                    {
                        await this._DownloadVolumeAsync(remoteAgentLocalPort, workloadInfo, volumeToken, localProcessConfig, cancellationToken);
                    }
                    perfLogger.SetProperty("ReferencedVolumesCount", localProcessConfig.ReferencedVolumes.Count());

                    // TODO: wait to support file token to actually enable this code.
                    //// download required files.
                    //foreach (var f in envParserResult.ReferencedFiles)
                    //{
                    //    string localPath = await this.DownloadFileAsync(workloadInfo, f.Name, f.LocalPath, progress, cancellationToken);
                    //    if (!string.IsNullOrEmpty(localPath))
                    //    {
                    //        envValues.AddFile(f.Name, localPath);
                    //    }
                    //}
                    //perfLogger.SetProperty("ReferencedFilesCount", envParserResult.ReferencedFiles.Count());

                    // Replace service reference IP address with mapped IP of the service
                    foreach (var envService in localProcessConfig.ReferencedServices)
                    {
                        foreach (var endpoint in workloadInfo.ReachableEndpoints)
                        {
                            if (StringComparer.OrdinalIgnoreCase.Equals(endpoint.DnsName, envService.Name) && endpoint.Ports.Any())
                            {
                                envService.IpAddress = endpoint.LocalIP.ToString();
                                break;
                            }
                        }
                    }
                    perfLogger.SetProperty("ReferencedServicesCount", localProcessConfig.ReferencedServices.Count());

                    // Update the environment variables
                    foreach (var env in localProcessConfig.EvaluateEnvVars())
                    {
                        workloadInfo.EnvironmentVariables[env.Key] = env.Value;
                    }
                    perfLogger.SetSucceeded();
                }
            }
        }

        /// <summary>
        /// Downloads file at given path in the remote agent container to specified local path
        /// </summary>
        private async Task<string> _DownloadFileAsync(
            int remoteAgentLocalPort,
            WorkloadInfo workloadInfo,
            string path,
            string localPath,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(localPath))
            {
                localPath = _fileSystem.Path.GetTempFilePath(Guid.NewGuid().ToString("N").Substring(0, 8));
            }
            if (!path.StartsWith("/"))
            {
                path = '/' + path;
            }

            // If container path is not mentioned, use remote agent API
            if (string.IsNullOrEmpty(workloadInfo.WorkingContainer))
            {
                var httpClient = new HttpClient()
                {
                    BaseAddress = new Uri($"http://{IPAddress.Loopback}:{remoteAgentLocalPort}")
                };
                this._ReportProgress(Resources.DownloadingFromFormat, path);

                string tarFile = _fileSystem.Path.GetTempFilePath(Guid.NewGuid().ToString("N") + ".tar.gz");
                string downloadPath = $"/api/download/files{path}";

                Exception downloadException = null;
                for (int retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        using (var downloadResponse = await httpClient.GetAsync(downloadPath))
                        {
                            downloadResponse.EnsureSuccessStatusCode();
                            using (var outputStream = _fileSystem.CreateFile(tarFile))
                            {
                                await downloadResponse.Content.CopyToAsync(outputStream);
                            }
                            _fileSystem.CreateDirectory(localPath);
                            var tarCommand = "tar";
                            var tarArguments = $"xzf {tarFile}";
                            var (exitCode, output) = this._platform.ExecuteAndReturnOutput(tarCommand,
                                                                                            tarArguments,
                                                                                            timeout: TimeSpan.FromSeconds(30),
                                                                                            stdOutCallback: (line) => _log.Info(line),
                                                                                            stdErrCallback: (line) => _log.Error(line),
                                                                                            workingDirectory: localPath);
                            if (exitCode != 0)
                            {
                                this._ReportProgress(Resources.RunningTarCommandFailed, tarCommand, tarArguments, exitCode, output);
                                _log.Warning($"Running {tarCommand} {tarArguments} failed with exit code {exitCode}: {output}");
                            }
                        }
                        this._ReportProgress(Resources.DownloadCompletedMessage);
                        break;
                    }
                    catch (Exception ex)
                    {
                        downloadException = ex;
                        _log.ExceptionAsWarning(ex);
                        this._ReportProgress(Resources.DownloadFailed, ex.Message);
                        this._ReportProgress(Resources.RetryingMessage, ex.Message);
                        await Task.Delay(1000);
                    }
                }
                if (downloadException != null)
                {
                    this._ReportProgress(Resources.DownloadFailed, downloadException.Message);
                }
            }
            // Container path is mentioned, use kubectl to copy
            else
            {
                this._CopyDirectoryFromContainerKubectl(workloadInfo, workloadInfo.WorkingContainer, localPath, cancellationToken);
            }

            return localPath;
        }

        /// <summary>
        /// Downloads mounted volume for the container
        /// </summary>
        private async Task<string> _DownloadVolumeAsync(
            int remoteAgentLocalPort,
            WorkloadInfo workloadInfo,
            IVolumeToken volumeToken,
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken)
        {
            ContainerVolumeMountInfo containerVolume = workloadInfo.VolumeMounts.Where(v => StringComparer.OrdinalIgnoreCase.Equals(volumeToken.Name, v.Name))
                                                                             .FirstOrDefault();
            if (containerVolume == null)
            {
                containerVolume = workloadInfo.VolumeMounts.Where(v => PatternMatchingUtillities.IsMatch(volumeToken.Name, v.Name)).FirstOrDefault();
            }
            if (containerVolume == null)
            {
                this._ReportProgress(EventLevel.Error, Resources.VolumeMountCannotBeFoundFormat, volumeToken.Name, Product.Name, localProcessConfig.ConfigFilePath);
                throw new UserVisibleException(_operationContext, Resources.VolumeMountCannotBeFoundFormat, new PII(volumeToken.Name), Product.Name, new PII(localProcessConfig.ConfigFilePath));
            }

            if (string.IsNullOrEmpty(volumeToken.LocalPath))
            {
                volumeToken.LocalPath = _fileSystem.Path.GetTempFilePath(Guid.NewGuid().ToString("N").Substring(0, 8));
            }

            this._ReportProgress(Resources.DownloadingVolumeFormat, containerVolume.Name, containerVolume.ContainerPath);
            return await this._DownloadFileAsync(remoteAgentLocalPort, workloadInfo, containerVolume.ContainerPath, volumeToken.LocalPath, cancellationToken);
        }

        /// <summary>
        /// Using kubectl copy container path to local path
        /// </summary>
        private void _CopyDirectoryFromContainerKubectl(
            WorkloadInfo workloadInfo,
            string containerPath,
            string localPath,
            CancellationToken cancellationToken)
        {
            localPath = _fileSystem.Path.GetFullPath(localPath);
            _fileSystem.CreateDirectory(localPath);

            // work around kubectl bug https://github.com/kubernetes/kubernetes/issues/77310 kubectl can't take Windows full path.
            string targetPath = _platform.IsWindows ? Guid.NewGuid().ToString() : localPath;

            int i = workloadInfo.WorkingContainer.LastIndexOf('/');
            string namespacePodName = workloadInfo.WorkingContainer.Substring(0, i);
            string containerName = workloadInfo.WorkingContainer.Substring(i + 1);
            var kubectlCommand = $"cp {namespacePodName}:{containerPath} {targetPath}";

            StringBuilder outputBuffer = new StringBuilder();
            int ret = _kubernetesClient.InvokeShortRunningKubectlCommand(KubernetesCommandName.Copy,
                                                      kubectlCommand,
                                                      onStdOut: output => outputBuffer.AppendLine(output),
                                                      onStdErr: output => outputBuffer.AppendLine(output),
                                                      shouldIgnoreErrors: true,
                                                      cancellationToken: cancellationToken);
            if (ret != 0)
            {
                this._ReportProgress(EventLevel.Warning, Resources.CopyContainerFailedFormat, containerPath, namespacePodName, localPath, ret);
                return;
            }
            else if (!StringComparer.OrdinalIgnoreCase.Equals(targetPath, localPath))
            {
                this._ReportProgress(EventLevel.Verbose, Resources.MovingContentsFromContainerToLocalPathFormat, targetPath, localPath);
                _fileSystem.MoveDirectory(targetPath, localPath);
            }
        }

        /// <summary>
        /// Ping endpoint manager every 30s to keep it running
        /// </summary>
        private void _KeepEndpointManagerAlive(IEndpointManagementClient endpointManagementClient)
        {
            if (_endpointManagerKeepAliveTimer == null)
            {
                TimeSpan pingInterval = TimeSpan.FromSeconds(30);  // ping the EndpointManager every 30 seconds to keep it alive.
                _endpointManagerKeepAliveTimer = new Timer((_) =>
                {
                    try
                    {
                        endpointManagementClient.PingEndpointManagerAsync(CancellationToken.None).Forget();
                    }
                    catch (Exception)
                    {
                    }
                }, null, pingInterval, pingInterval);
            }
        }

        #endregion Private methods
    }
}