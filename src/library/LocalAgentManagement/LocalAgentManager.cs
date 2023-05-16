// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Models;
using YamlDotNet.RepresentationModel;
using static Microsoft.BridgeToKubernetes.Common.Constants;

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
            /*// Serialize config to temp file
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

            // expose a PORT for the local agent to listen on
            commandLine.Append($"-p 54411:7891 ");

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
            commandLine.Append("hsubramanian/localagent:v15");

            this.RunDockerCommand(commandLine.ToString());*/
            var commandLine = new StringBuilder();
            string filePath = _fileSystem.Path.Combine("Files", "docker-compose.yml");
            AssertHelper.True(_fileSystem.FileExists(filePath), $"docker compose file is missing: '{filePath}'");
            commandLine.Append("-p localagent ");
            commandLine.Append($"-f \"{filePath}\" ");
            commandLine.Append("up -d");
            this.RunDockerComposeUpCommand(commandLine.ToString());
        }

        /*public void startLocalAgentUsingDockerCompose(LocalAgentConfig config, KubeConfigDetails kubeConfigDetails)
        {
            // createDockerCompose();
            // createDevContainer - docker for desktop is required
            // use devcontainer cli to build and up the containers. 
            // string content = _fileSystem.ReadAllTextFromFile("Files\\docker-compose.yml");
            var commandLine = new StringBuilder();
            commandLine.Append("-f Files\\docker-compose.yml");
            commandLine.Append("--build");
            commandLine.Append("up");
            this.RunDockerComposeUpCommand(commandLine.ToString());
        }*/

        /// <summary>
        /// <see cref="ILocalAgentManagementClient.StopLocalAgent"/>
        /// </summary>
        public void StopLocalAgent()
        {
            //docker compose down
            var commandLine = new StringBuilder();
            string filePath = _fileSystem.Path.Combine("Files", "docker-compose.yml");
            AssertHelper.True(_fileSystem.FileExists(filePath), $"docker compose file is missing: '{filePath}'");
            commandLine.Append("-p localagent ");
            commandLine.Append($"-f \"{filePath}\" ");
            commandLine.Append("down");
            this.RunDockerComposeUpCommand(commandLine.ToString());
            // remove the image
            if (!string.IsNullOrEmpty(_localAgentContainerName))
            {
                this.RunDockerCommand($"rm -f {_localAgentContainerName}", throwWithFailure: false);
            }
        }

        private int RunDockerCommand(string command, out string output)
        {
            return _platform.Execute("docker", command, (message) => _log.Verbose(message), envVariables: null, timeout: TimeSpan.FromSeconds(10), cancellationToken: CancellationToken.None, out output);
        }

        private int RunDockerComposeCommand(string command, out string output)
        {
            string dockerComposeFilePath = @"C:\Program Files\Docker\Docker\resources\bin\docker-compose.exe";
            return _platform.Execute(dockerComposeFilePath, command, (message) => _log.Verbose(message), envVariables: null, timeout: TimeSpan.FromSeconds(30), cancellationToken: CancellationToken.None, out output);
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

        private string RunDockerComposeUpCommand(string command, bool throwWithFailure = true)
        {
            StringBuilder sb = new StringBuilder();
            int exitCode = this.RunDockerComposeCommand(command, out string output);
            if (throwWithFailure && exitCode != 0)
            {
                throw new InvalidOperationException($"docker {command} failed, exit code {exitCode}. {sb.ToString()}");
            }
            return output;
        }
    }
}