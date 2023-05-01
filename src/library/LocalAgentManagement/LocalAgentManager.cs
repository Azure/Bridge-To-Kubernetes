// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.LocalAgentManagement
{
    internal class LocalAgentManager : ILocalAgentManager
    {
        private readonly IFileSystem _fileSystem;
        private readonly IPlatform _platform;
        private readonly ILog _log;
        private string _localAgentContainerName;

        public delegate ILocalAgentManager Factory(string localAgentContainerName);

        public LocalAgentManager(
            string localAgentContainerName,
            IFileSystem fileSystem,
            IPlatform platform,
            ILog log)
        {
            _localAgentContainerName = localAgentContainerName;
            _platform = platform;
            _fileSystem = fileSystem;
            _log = log;
        }

        /// <summary>
        /// <see cref="ILocalAgentManagementClient.StartLocalAgent(LocalAgentConfig, string, string)"/>
        /// </summary>
        public void StartLocalAgent(LocalAgentConfig config, KubeConfigDetails kubeConfigDetails)
        {
            // Serialize config to temp file
            string localAgentConfigFilePath = _fileSystem.Path.GetTempFilePath();
            var content = JsonHelpers.SerializeObject(config);
            _fileSystem.WriteAllTextToFile(localAgentConfigFilePath, content);

            var commandLine = new StringBuilder();
            commandLine.Append($"run -dit --name {_localAgentContainerName} ");

            // Add the file mount for the LocalAgent config
            commandLine.Append($"-v \"{localAgentConfigFilePath}:{Common.Constants.LocalAgent.LocalAgentConfigPath}\" ");

            // Add the file mount for the kubeConfig
            commandLine.Append($"-v \"{kubeConfigDetails.Path}:{Common.Constants.LocalAgent.KubeConfigPath}\" ");
            commandLine.Append($"--env \"KUBECONFIG={Common.Constants.LocalAgent.KubeConfigPath}\" ");

            // Add the NET_ADMIN privileges: this is used to run iptables
            commandLine.Append($"--cap-add=NET_ADMIN ");

            // Create --add-host arguments
            foreach (var endpoint in config.ReachableEndpoints)
            {
                endpoint.ValidateDnsName();
                var serviceAliases = endpoint.GetServiceAliases(config.RemoteAgentInfo.NamespaceName, this._log);
                foreach (var serviceAlias in serviceAliases)
                {
                    commandLine.Append($"--add-host \"{serviceAlias}:{endpoint.LocalIP}\" ");
                }
            }
            commandLine.Append("hsubramanian/localagent:v3");

            this.RunDockerCommand(commandLine.ToString());
        }

        /// <summary>
        /// <see cref="ILocalAgentManagementClient.StopLocalAgent"/>
        /// </summary>
        public void StopLocalAgent()
        {
            if (!string.IsNullOrEmpty(_localAgentContainerName))
            {
                this.RunDockerCommand($"rm -f {_localAgentContainerName}", throwWithFailure: false);
            }
        }

        private int RunDockerCommand(string command, out string output)
        {
            return _platform.Execute("docker", command, (message) => _log.Verbose(message), envVariables: null, timeout: TimeSpan.FromSeconds(10), cancellationToken: CancellationToken.None, out output);
        }

        private string RunDockerCommand(string command, bool throwWithFailure = true)
        {
            StringBuilder sb = new StringBuilder();
            int exitCode = this.RunDockerCommand(command, out string output);
            if (throwWithFailure && exitCode != 0)
            {
                throw new InvalidOperationException($"docker {command} failed, exit code {exitCode}. {sb.ToString()}");
            }
            return output;
        }
    }
}