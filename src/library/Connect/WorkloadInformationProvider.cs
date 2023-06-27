// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;
using Microsoft.BridgeToKubernetes.Common.Models.Settings;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.Logging;
using Microsoft.BridgeToKubernetes.Library.Models;
using static Microsoft.BridgeToKubernetes.Library.Constants;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    internal class WorkloadInformationProvider : IWorkloadInformationProvider
    {
        private readonly IKubernetesClient _kubernetesClient;
        private readonly ILog _log;
        private readonly IOperationContext _operationContext;
        private readonly IResourceNamingService _resourceNamingService;
        private AsyncLazy<RemoteContainerConnectionDetails> _remoteContainerConnectionDetails;
        private readonly IProgress<ProgressUpdate> _progress;

        public delegate IWorkloadInformationProvider Factory(IKubernetesClient kubernetesClient, AsyncLazy<RemoteContainerConnectionDetails> connectionDetails);

        public WorkloadInformationProvider(
            IKubernetesClient kubernetesClient,
            IResourceNamingService resourceNamingService,
            AsyncLazy<RemoteContainerConnectionDetails> connectionDetails,
            IProgress<ProgressUpdate> progress,
            ILog log,
            IOperationContext operationContext)
        {
            this._kubernetesClient = kubernetesClient;
            this._resourceNamingService = resourceNamingService;
            this._remoteContainerConnectionDetails = connectionDetails;
            this._progress = progress;
            this._log = log;
            this._operationContext = operationContext;

            // TODO(ansoedal): AsyncLazy needs to be modified so that we can pass the Cancellation token in at a later time
            this.WorkloadEnvironment = new AsyncLazy<IDictionary<string, string>>(async () => await this.GetWorkloadContainerEnvironmentAsync(CancellationToken.None));
        }

        public AsyncLazy<IDictionary<string, string>> WorkloadEnvironment { get; }

        /// <summary>
        /// <see cref="IWorkloadInformationProvider.GetReachableEndpointsAsync"/>
        /// </summary>
        // TODO (lolodi): This method should already take care of avoiding possible duplicates between services that are already reachable and the ones defined by the user in the localProcessConfig.
        // TODO (lolodi)(perf): This result of this method should be wrapped in an AsyncLazy so that we only pull it once
        public async Task<IEnumerable<EndpointInfo>> GetReachableEndpointsAsync(
            string namespaceName,
            ILocalProcessConfig localProcessConfig,
            bool includeSameNamespaceServices,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.WorkloadInformationProvider.AreaName, Events.WorkloadInformationProvider.Operations.GetReachableEndpoints))
            {
                var reachableEndpoints = new List<EndpointInfo>();
                var servicesToRoute = new List<V1Service>();
                // Dict to hold separate list of ports to ignore for each service.
                var portsToIgnore = new Dictionary<String, IList<int>>();

                if (includeSameNamespaceServices)
                {
                    var servicesInNamespace = await _kubernetesClient.ListServicesInNamespaceAsync(namespaceName, cancellationToken: cancellationToken);
                    // check if there are any ports that need to be ignored
                    portsToIgnore = GetPortsToIgnoreFromAnnotations(servicesInNamespace);
                    servicesToRoute.AddRange(servicesInNamespace.Items);
                }

                if (localProcessConfig != null)
                {
                    // Add any referenced services from the LocalProcessConfig
                    var localProcessConfigServices = await _CollectLocalConfigServices(
                        localProcessConfig: localProcessConfig,
                        cancellationToken: cancellationToken
                    );
                    servicesToRoute = servicesToRoute.Union(localProcessConfigServices, new V1ServiceEqualityComparer()).ToList();

                    // Add any referenced external endpoints
                    foreach (var externalEndpoint in localProcessConfig.ReferencedExternalEndpoints)
                    {
                        // Referenced dependency is always an external dns-name with port number.
                        if (externalEndpoint.Ports == null || !externalEndpoint.Ports.Any())
                        {
                            throw new UserVisibleException(_operationContext, Resources.PortRequiredFormat, externalEndpoint.Name);
                        }
                        reachableEndpoints.Add(new EndpointInfo()
                        {
                            DnsName = externalEndpoint.Name,
                            // Here we are not taking into account the ports to ignore, since this code path is handling endpoints user explicitly added for tracking.
                            Ports = externalEndpoint.Ports.Select(p => new PortPair(remotePort: p)).ToArray(),
                            IsExternalEndpoint = true,
                            IsInWorkloadNamespace = false
                        });
                    }
                }

                var servicesToRouteEndpointInfos = await this._CollectServicesToRouteAsync(namespaceName, servicesToRoute, cancellationToken, portsToIgnore);
                reachableEndpoints.AddRange(servicesToRouteEndpointInfos);

                AddEndpointInfoForManagedIdentityIfRequired(localProcessConfig?.IsManagedIdentityScenario ?? false, namespaceName, reachableEndpoints);

                perfLogger.SetSucceeded();
                return reachableEndpoints;
            }
        }

        public Dictionary<String, IList<int>> GetPortsToIgnoreFromAnnotations(V1ServiceList serviceNameSpace) {
            Dictionary<String, IList<int>> servicePortsToIgnore = new Dictionary<String, IList<int>>();
            List<V1Service> servicesWithIgnorePorts = serviceNameSpace.Items.Where(item => item.Metadata?.Annotations?.ContainsKey(DeploymentConfig.ServiceAnnotations) ?? false).ToList();
            servicesWithIgnorePorts.ForEach(service => {
                if (service.Metadata?.Annotations?.TryGetValue(DeploymentConfig.ServiceAnnotations, out string ports) ?? false) {
                    if (ports.Length > 0) {
                        try {
                            List<int> ignorePorts = ports.Split(",").Select(p => int.Parse(p)).ToList();
                            servicePortsToIgnore.Add(service.Metadata.Name, ignorePorts);
                        }
                        catch {
                            throw new UserVisibleException(_operationContext, $"bridgetokubernetes/ignore-ports configuration value {ports} is invalid. It must be a comma separated list of integer ports");
                        }
                    }
                }
            });
            return servicePortsToIgnore;
        }

        /// <summary>
        /// <see cref="IWorkloadInformationProvider.GatherWorkloadInfo"/>
        /// </summary>
        public async Task<WorkloadInfo> GatherWorkloadInfo(
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(Events.WorkloadInformationProvider.AreaName, Events.WorkloadInformationProvider.Operations.GatherWorkloadInfo))
            {
                var remoteContainerConnectionDetails = await _remoteContainerConnectionDetails;
                var container = remoteContainerConnectionDetails.Container ?? throw new ArgumentNullException("The container should always be resolved before proceding to establish a connection");

                WorkloadInfo workloadInfo = new WorkloadInfo()
                {
                    EnvironmentVariables = new Dictionary<string, string>(),
                    ReversePortForwardInfo = new List<PortForwardStartInfo>(),
                    VolumeMounts = new List<ContainerVolumeMountInfo>(),
                    ReachableEndpoints = new List<EndpointInfo>(),
                    Namespace = remoteContainerConnectionDetails.NamespaceName,
                    WorkloadName = remoteContainerConnectionDetails.ServiceName ?? remoteContainerConnectionDetails.DeploymentName ?? remoteContainerConnectionDetails.PodName
                };

                remoteContainerConnectionDetails.Container?.VolumeMounts?.ExecuteForEach(v =>
                {
                    workloadInfo.VolumeMounts = workloadInfo.VolumeMounts.Append(new ContainerVolumeMountInfo()
                    {
                        Name = v.Name,
                        LocalVolumeName = _resourceNamingService.GetVolumeName(v.Name),
                        ContainerPath = v.MountPath,
                        SubPath = v.SubPath
                    });
                });

                List<string> args = new List<string>();
                if (container.Command?.Any() ?? false)
                {
                    workloadInfo.Entrypoint = container.Command.First();
                    args.AddRange(container.Command.Skip(1));
                }
                if (container?.Args != null)
                {
                    args.AddRange(container.Args);
                }
                workloadInfo.Args = args.ToArray();

            workloadInfo.EnvironmentVariables = await this.WorkloadEnvironment;
            var isDapr = workloadInfo.EnvironmentVariables.ContainsKey("DAPR_HTTP_PORT") &&
                    workloadInfo.EnvironmentVariables.ContainsKey("DAPR_GRPC_PORT");

                workloadInfo.ReachableEndpoints = await this.GetReachableEndpointsAsync(remoteContainerConnectionDetails.NamespaceName, localProcessConfig, includeSameNamespaceServices: !isDapr, cancellationToken);

                #region localPorts

                // Ports exposed by the user workload
                IEnumerable<int> ports = new List<int>();
                if (remoteContainerConnectionDetails.Service != null)
                {
                    var servicePorts = remoteContainerConnectionDetails.Service.Spec?.Ports?.Select(p => GetServicePortValue(p, container)) ?? new List<int>();
                    // Check for DAPR
                    if (isDapr)
                    {
                        // We are targeting a DAPR container, it might be that the service is pointing to the user's workload container, or it might be pointing to the sidecar
                        // If there are ports that are both in the service and the container we might assume that the service is pointing to the user's workload
                        ports = servicePorts.Intersect(container.Ports?.Select(p => p.ContainerPort) ?? new int[0]);
                        if (ports.Any())
                        {
                            // The service is actually targeting the user's workload, let's use all the services port.
                            ports = servicePorts;
                        }
                        else
                        {
                            // The service is actually targeting the DAPR sidecar, we should fetch the port from the container spec
                            // NOTE: it is not required to declare the ports in the container spec, let's hope that the user did that otherwise there is no way for us to know what ports to map
                            ports = container.Ports?.Select(p => p.ContainerPort) ?? new List<int>();
                            if (!ports.Any())
                            {
                                _log.Info("No container ports found");
                            }
                        }
                    }
                    else
                    {
                        // NO DAPR, behave as usual
                        ports = servicePorts;
                    }
                }
                else
                {
                    if (remoteContainerConnectionDetails.Pod != null)
                    {
                        // We don't have a service, but we do have a Pod
                        var services = await GetServicesPointingToPod(remoteContainerConnectionDetails.Pod, cancellationToken);
                        ports = services.SelectMany(svc => svc.Spec.Ports).Distinct().Select(p => GetServicePortValue(p, container));
                    }
                    else if (remoteContainerConnectionDetails.Deployment != null)
                    {
                        var services = await GetServicesFromDeployment(remoteContainerConnectionDetails.Deployment, cancellationToken);
                        ports = services.SelectMany(svc => svc.Spec.Ports).Distinct().Select(p => GetServicePortValue(p, container));
                    }
                }
                if (!ports.Any())
                {
                    _log.Info("The remote workload is not exposing any port");
                }
                foreach (var port in ports)
                {
                    var cp = new PortForwardStartInfo();
                    cp.Port = port;

                    List<string> probes = new List<string>();
                    if (container.LivenessProbe?.HttpGet != null)
                    {
                        probes.Add(container.LivenessProbe.HttpGet.Path);
                    }
                    if (container.ReadinessProbe?.HttpGet != null)
                    {
                        probes.Add(container.ReadinessProbe.HttpGet.Path);
                    }
                    cp.HttpProbes = probes.Distinct().ToArray();
                    workloadInfo.ReversePortForwardInfo = workloadInfo.ReversePortForwardInfo.Append(cp);
                }

                #endregion localPorts

                // Check for DAPR

                if (workloadInfo.EnvironmentVariables.TryGetValue("DAPR_HTTP_PORT", out string httpPort) &&
                    workloadInfo.EnvironmentVariables.TryGetValue("DAPR_GRPC_PORT", out string grpcPort))
                {
                    _log.Event("DaprDetected", new Dictionary<string, object> { { "DAPR_HTTP_PORT", httpPort }, { "DAPR_GRPC_PORT", grpcPort } });
                    int httpPortInt, grpcPortInt;
                    try
                    {
                        httpPortInt = int.Parse(httpPort);
                        grpcPortInt = int.Parse(grpcPort);
                    }
                    catch (Exception ex)
                    {
                        _log.Exception(ex);
                        throw new UserVisibleException(_operationContext, $"Unable to parse DAPR ports: {ex.Message}");
                    }
                    workloadInfo.ReachableEndpoints = workloadInfo.ReachableEndpoints.Append(new EndpointInfo
                    {
                        Ports = new PortPair[] {
                                new PortPair(Common.Constants.IP.PortPlaceHolder, httpPortInt), //NOTE: The order in this array is important as LocalEnvironmentManager relies on it.
                                new PortPair(Common.Constants.IP.PortPlaceHolder, grpcPortInt) },
                        DnsName = Common.Constants.DAPR,
                        LocalIP = IPAddress.Loopback
                    });
                }
                perfLogger.SetSucceeded();

                return workloadInfo;
            }
        }

        #region Private members

        /// <summary>
        /// Progress reporter for <see cref="WorkloadInformationProvider"/>
        /// </summary>
        private void ReportProgress(string message, params object[] args)
        {
            this._progress.Report(new ProgressUpdate(0, ProgressStatus.WorkloadInformationProvider, new ProgressMessage(EventLevel.Informational, _log.SaferFormat(message, args))));
        }

        private int GetServicePortValue(V1ServicePort servicePort, V1Container container)
        {
            int parsedPort = -1;
            if (!int.TryParse(servicePort.TargetPort.Value, out parsedPort))
            {
                // if we get this it is beacuse it is not an int, we ned to go look in the container spec to see what port has this name
                parsedPort = container.Ports?.Where(containerPort => StringComparer.OrdinalIgnoreCase.Equals(containerPort.Name, servicePort.TargetPort.Value)).FirstOrDefault()?.ContainerPort ?? -1;
            }
            return parsedPort;
        }

        private async Task<IDictionary<string, string>> GetWorkloadContainerEnvironmentAsync(CancellationToken cancellationToken)
        {
            var remoteContainerConnectionDetails = await _remoteContainerConnectionDetails;
            var container = remoteContainerConnectionDetails.Container;
            // Gather all the environment variable names defined in the Container spec (including config maps and secrets in the EnvFrom)
            List<string> envNames = new List<string>();
            var specValues = new Dictionary<string, string>();
            if (container?.Env != null)
            {
                envNames.AddRange(container.Env.Select(env => env.Name));
                foreach (var env in container.Env)
                {
                    specValues.TryAdd(env.Name, new string(env.Value));
                }
            }
            if (container?.EnvFrom != null)
            {
                foreach (var envFrom in container.EnvFrom)
                {
                    if (!string.IsNullOrWhiteSpace(envFrom.ConfigMapRef?.Name))
                    {
                        var configMap = await this._kubernetesClient.GetConfigMapAsync(remoteContainerConnectionDetails.NamespaceName, envFrom.ConfigMapRef.Name, cancellationToken);
                        if (configMap?.Data != null)
                        {
                            envNames.AddRange(configMap.Data.Keys);
                            foreach (var env in configMap.Data)
                            {
                                specValues.TryAdd(env.Key, new string(env.Value));
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(envFrom.SecretRef?.Name))
                    {
                        var secret = await this._kubernetesClient.ReadNamespacedSecretAsync(remoteContainerConnectionDetails.NamespaceName, envFrom.SecretRef.Name, cancellationToken);
                        if (secret?.Data != null)
                        {
                            envNames.AddRange(secret.Data.Keys);
                        }
                    }
                }
            }
            IDictionary<string, string> containerEnvironment;
            var result = new Dictionary<string, string>();
            try
            {
                // Pull the environment variables from the remote container
                containerEnvironment = await _kubernetesClient.GetContainerEnvironmentAsync(remoteContainerConnectionDetails.NamespaceName, remoteContainerConnectionDetails.PodName, remoteContainerConnectionDetails.ContainerName, cancellationToken);
            }
            catch (UserVisibleException ex)
            {
                if (!string.IsNullOrWhiteSpace(ex.Message) && ex.Message.Contains("executable file not found in $PATH", StringComparison.OrdinalIgnoreCase))
                {
                    ReportProgress(CommonResources.FailedToGetTheContainerEnvironmentMinimalImageFormat, new PII(remoteContainerConnectionDetails.ContainerName));
                }
                else
                {
                    ReportProgress(ex.Message);
                }
                ReportProgress(CommonResources.DefaultingToContainerSpecEnvironmentVariables);
                return specValues;
            }

            // Adding all the KUBERNETES environment variable, regardless if they are mentioned in the container spec or not.
            foreach (var envName in containerEnvironment)
            {
                if (envName.Key.StartsWith("KUBERNETES_"))
                {
                    result[envName.Key] = envName.Value;
                }
            }

            // Get all the rest of the environment variables.
            foreach (var envName in envNames)
            {
                if (containerEnvironment.TryGetValue(envName, out string value))
                {
                    result[envName] = value;
                }
            }
            return result;
        }

        /// <summary>
        /// Enumerates Kubernetes services that should be routed locally by including services that specify a port and returns a namespace to service list map.
        /// </summary>
        private async Task<IEnumerable<EndpointInfo>> _CollectServicesToRouteAsync(
            string workloadNamespace,
            IEnumerable<V1Service> services,
            CancellationToken cancellationToken,
            Dictionary<String, IList<int>> portsToIgnoreForService)
        {
            var servicesToRouteMap = new ConcurrentDictionary<string, V1Service>();
            var headlessServiceEndpointsToRouteMap = new ConcurrentDictionary<string, V1Endpoints>();

            Func<string, string, string> getMapKey = (string name, string namespaceName) => $"{name}.{namespaceName}";

            Dictionary<string, IList<int>> portToIgnoreForHeadlessServiceEndpoints = new Dictionary<string, IList<int>>();

            foreach (var s in services)
            {
                // Do not consider the routing manager service in list of reachable services
                if (StringComparer.OrdinalIgnoreCase.Equals(s.Metadata.Name, Routing.RoutingManagerServiceName))
                {
                    continue;
                }
                // If a service is of type ClusterIP but has "None" as ClusterIP it's an headless service
                if (StringComparer.OrdinalIgnoreCase.Equals(s.Spec.Type, "ClusterIP") && StringComparer.OrdinalIgnoreCase.Equals(s.Spec.ClusterIP, "None") && (s.Spec.Ports?.Any() ?? false))
                {
                    _log.Verbose("Detected headless service. Looking for endpoints with same name as service...");
                    var endpoint = await _kubernetesClient.GetEndpointInNamespaceAsync(s.Metadata.Name, s.Metadata.NamespaceProperty, cancellationToken: cancellationToken);

                    if (endpoint == null)
                    {
                        _log.Warning($"Failed to resolve any endpoints with the same name as headless service: '{new PII(s.Metadata.Name)}'");
                        continue;
                    }

                    if (endpoint.Subsets == null) {
                        _log.Info($"Skipping endpoint: '{endpoint.Metadata.Name}' for service '{s.Metadata.Name}' because it has empty subsets.");
                        continue;
                    }

                    if (!headlessServiceEndpointsToRouteMap.ContainsKey(getMapKey(endpoint.Metadata.Name, endpoint.Metadata.NamespaceProperty)))
                    {
                        headlessServiceEndpointsToRouteMap.TryAdd(getMapKey(endpoint.Metadata.Name, endpoint.Metadata.NamespaceProperty), endpoint);
                        // Map portsToIgnore from Service.Metadata.Name to Endpoints.Metadata.Name
                        if (portsToIgnoreForService.ContainsKey(s.Metadata.Name)) {
                            portToIgnoreForHeadlessServiceEndpoints.Add(endpoint.Metadata.Name, portsToIgnoreForService.GetValueOrDefault(s.Metadata.Name));
                        }
                        continue;
                    }
                }

                if ((s.Spec.Type == "ClusterIP" || s.Spec.Type == "LoadBalancer" || s.Spec.Type == "NodePort") && !string.IsNullOrWhiteSpace(s.Spec.ClusterIP) && (s.Spec.Ports?.Any() ?? false))
                {
                    if (!servicesToRouteMap.ContainsKey(getMapKey(s.Metadata.Name, s.Metadata.NamespaceProperty)))
                    {
                        servicesToRouteMap.TryAdd(getMapKey(s.Metadata.Name, s.Metadata.NamespaceProperty), s);
                    }
                }
            }

            var servicesToRouteEndpointInfos = servicesToRouteMap.Values.Select(s => new EndpointInfo()
            {
                DnsName = StringComparer.OrdinalIgnoreCase.Equals(s.Metadata.Namespace(), workloadNamespace) ?
                            s.Metadata.Name :
                            $"{s.Metadata.Name}.{s.Metadata.Namespace()}",
                // portsToIgnore is a directory which contains a separate list of ports to ignore for each service, when picking ports for a service any port which is in the portsToIgnore is not used.
                Ports = s.Spec.Ports?
                    .Where(p => this._IsSupportedProtocol(p.Protocol, s.Metadata.Name) && !(portsToIgnoreForService.GetValueOrDefault(s.Metadata.Name)?.Contains(p.Port) ?? false))
                    .Select(p => new PortPair(remotePort: p.Port, name: p.Name))
                    .ToArray() ?? new PortPair[] { },
                IsInWorkloadNamespace = StringComparer.OrdinalIgnoreCase.Equals(s.Metadata.Namespace(), workloadNamespace)
            }).ToList();

            // Add headless services info
            foreach (V1Endpoints endpoint in headlessServiceEndpointsToRouteMap.Values)
            {
                var isInWorkloadNamespace = StringComparer.OrdinalIgnoreCase.Equals(endpoint.Metadata.Namespace(), workloadNamespace);
                foreach (var subset in endpoint.Subsets)
                {
                    if (subset.Addresses == null)
                    {
                        continue;
                    }
                    
                    foreach (var address in  subset.Addresses)
                    {
                        if (address == null) {
                            continue;
                        }
                        string dns = "";
                        // If hostname is empty for the address, then dns used is that of the endpoint
                        if (!string.IsNullOrWhiteSpace(address.Hostname))
                        {
                            dns = isInWorkloadNamespace ?
                                    $"{address.Hostname}.{endpoint.Metadata.Name}" :
                                    $"{address.Hostname}.{endpoint.Metadata.Name}.{endpoint.Metadata.Namespace()}";
                        }
                        else
                        {
                            dns = isInWorkloadNamespace ? 
                                    endpoint.Metadata.Name : 
                                    $"{endpoint.Metadata.Name}.{endpoint.Metadata.Namespace()}";
                        }
                        servicesToRouteEndpointInfos.Add(new EndpointInfo()
                        {
                            DnsName = dns,
                            Ports = subset.Ports?
                                .Where(port => this._IsSupportedProtocol(port.Protocol, endpoint.Metadata.Name) && !(portToIgnoreForHeadlessServiceEndpoints.GetValueOrDefault(endpoint.Metadata.Name)?.Contains(port.Port) ?? false))
                                .Select(p => new PortPair(remotePort: p.Port,name : p.Name))
                                .ToArray() ?? new PortPair[] { },
                            IsInWorkloadNamespace = isInWorkloadNamespace
                        });
                    }
                }
            }

            return servicesToRouteEndpointInfos;
        }

        private async Task<IEnumerable<V1Service>> _CollectLocalConfigServices(
            ILocalProcessConfig localProcessConfig,
            CancellationToken cancellationToken)
        {
            var localConfigReachableServices = new List<V1Service>();
            foreach (var svc in localProcessConfig.ReferencedServices)
            {
                string referencedServiceName = svc.Name;
                string referencedServiceNamespace = string.Empty;

                string[] parts = referencedServiceName.Split('.');
                if (parts.Length == 2)
                {
                    referencedServiceName = parts[0];
                    referencedServiceNamespace = parts[1];
                }

                if (string.IsNullOrWhiteSpace(referencedServiceNamespace))
                {
                    referencedServiceNamespace = (await _remoteContainerConnectionDetails).NamespaceName;
                }

                V1Service service = null;
                try
                {
                    service = await _kubernetesClient.GetV1ServiceAsync(referencedServiceNamespace, referencedServiceName, cancellationToken: cancellationToken);
                }
                catch (Exception)
                {
                    // Service not found
                    _log.Warning($"Failed to find service '{referencedServiceName}'.");
                }
                if (service != null)
                {
                    localConfigReachableServices.Add(service);
                }
            }

            return localConfigReachableServices;
        }

        private void AddEndpointInfoForManagedIdentityIfRequired(bool isManagedIdentityScenario, string namespaceName, List<EndpointInfo> reachableEndpoints)
        {
            _operationContext.LoggingProperties[LoggingConstants.Property.IsManagedIdentityEnabled] = isManagedIdentityScenario;
            if (isManagedIdentityScenario)
            {
                reachableEndpoints.Add(new EndpointInfo()
                {
                    // We need to handle this case specially because we need to fake the presence of managed identity service in the same namespace
                    DnsName = $"{ManagedIdentity.TargetServiceNameOnLocalMachine}",
                    Ports = new PortPair[] { new PortPair(80) },
                    IsInWorkloadNamespace = true
                });
            }
        }

        /// <summary>
        /// Checks if the protocol used by the service is supported.
        /// </summary>
        private bool _IsSupportedProtocol(string protocol, string serviceName)
        {
            bool isSupported = (StringComparer.OrdinalIgnoreCase.Equals(protocol, "http") ||
                                StringComparer.OrdinalIgnoreCase.Equals(protocol, "tcp"));
            if (!isSupported)
            {
                _log.Warning($"Unsupported protocol '{protocol}' identified in service '{serviceName}'");
            }
            return isSupported;
        }

        // This method is called when the user did not specify a service. This doesn't happen today since the only way to connect from our clients is to specify a service
        private async Task<List<V1Service>> GetServicesPointingToPod(
            V1Pod pod,
            CancellationToken cancellationToken
            )
        {
            if (pod.Labels() == null)
            {
                _log.Info("Pod doesn't have any labels, unable to determine service backing pod.");
                return new List<V1Service>();
            }
            var svcs = await _kubernetesClient.ListServicesInNamespaceAsync(pod.Metadata.NamespaceProperty, null, cancellationToken);
            var result = svcs.Items.Where(svc => svc.Spec?.Selector?.IsSubsetOf(pod.Labels()) ?? false).ToList();

            if (result == null || !result.Any())
            {
                _log.Warning("Unable to find any service backing the pod {0}", new PII(pod.Metadata.Name));
            }
            _log.Info("Found {0} services backing pod {1}", result.Count(), new PII(pod.Metadata.Name));

            return result;
        }

        // This method is called when the user did not specify a service. This doesn't happen today since the only way to connect form our clients is to specify a service
        private async Task<List<V1Service>> GetServicesFromDeployment(
            V1Deployment deployment,
            CancellationToken cancellationToken
            )
        {
            var svcs = await _kubernetesClient.ListServicesInNamespaceAsync(deployment.Metadata.NamespaceProperty, null, cancellationToken);
            var result = svcs.Items.Where(svc => svc.Spec.Selector.IsSubsetOf(deployment.Spec.Template.Metadata.Labels)).ToList();

            if (result == null || !result.Any())
            {
                _log.Warning("Unable to find any service backing the deployment {0}", new PII(deployment.Metadata.Name));
            }
            _log.Info("Found {0} services backing pod {1}", result.Count(), new PII(deployment.Metadata.Name));

            return result;
        }

        private class V1ServiceEqualityComparer : IEqualityComparer<V1Service>
        {
            public bool Equals(V1Service s1, V1Service s2)
            {
                if (s1 == null && s2 == null)
                {
                    return true;
                }
                else if (s1 == null || s2 == null)
                {
                    return false;
                }
                else if (StringComparer.OrdinalIgnoreCase.Equals(s1.Name(), s2.Name()) &&
                        StringComparer.OrdinalIgnoreCase.Equals(s1.Namespace(), s2.Namespace()))
                {
                    return true;
                }
                return false;
            }

            public int GetHashCode(V1Service service)
            {
                string hCode = $"{service.Namespace()}-{service.Name()}";
                return hCode.GetHashCode();
            }
        }

        #endregion Private members
    }
}
