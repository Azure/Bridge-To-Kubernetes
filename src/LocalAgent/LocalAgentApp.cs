// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.IP;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.Extensions.Hosting;

namespace Microsoft.BridgeToKubernetes.LocalAgent
{
    internal class LocalAgentApp : IHostedService
    {
        private readonly LocalAgentConfig _config;
        private readonly IIPManager _ipManager;
        private readonly ILog _log;
        private IProgress<ProgressUpdate> _progress;
        private readonly IConsoleOutput _out;
        private readonly IConnectManagementClient _connectManagementClient;
        private readonly IPortMappingManager _portMappingManager;

        public LocalAgentApp(
            IManagementClientFactory managementClientFactory,
            IIPManager ipManager,
            ILog log,
            IConsoleOutput consoleOutput,
            IFileSystem fileSystem,
            IPortMappingManager portMappingManager)
        {
            _ipManager = ipManager;
            _log = log;
            _out = consoleOutput;
            _progress = new SerializedProgress<ProgressUpdate>((progressUpdate) =>
            {
                if (progressUpdate.ShouldPrintMessage)
                {
                    _out.Info(progressUpdate.ProgressMessage.Message, progressUpdate.ProgressMessage.NewLine);
                }
            });

            try
            {
                _config = JsonHelpers.DeserializeObject<LocalAgentConfig>(fileSystem.ReadAllTextFromFile(Common.Constants.LocalAgent.LocalAgentConfigPath));
                //_config = JsonHelpers.DeserializeObject<LocalAgentConfig>(fileSystem.ReadAllTextFromFile("/home/hsubramanian/endPointInfo.json"));
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
                throw;
            }
            // TODO (lolodi): management clients need to be disposed for log flushing
            var kubeConfigManagementClient = managementClientFactory.CreateKubeConfigClient();
            var kubeConfigDetails = kubeConfigManagementClient.GetKubeConfigDetails();
            _connectManagementClient = managementClientFactory.CreateConnectManagementClient(null, kubeConfigDetails, false, false);
            _portMappingManager = portMappingManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            // assign local Ips for reachable services
            _config.ReachableEndpoints = _portMappingManager.AddLocalPortMappings(_config.ReachableEndpoints);
            // Add iptables rules
            _ipManager.AllocateIPs(_config.ReachableEndpoints, addRoutingRules: true, cancellationToken);

            // Start service port forward
            int remoteAgentLocalPort = await _connectManagementClient.ConnectToRemoteAgentAsync(_config.RemoteAgentInfo, cancellationToken);
            await _connectManagementClient.StartServicePortForwardingsAsync(remoteAgentLocalPort, _config.ReachableEndpoints, _config.ReversePortForwardInfo, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            // We shouldn't need to do nothing here because the RemoteAgent get's restored in the bridge process.
            return Task.CompletedTask;
        }
    }
}