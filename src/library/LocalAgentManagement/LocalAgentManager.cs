// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.Docker;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Models;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization.NamingConventions;
using YamlDotNet.Serialization;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using System.Diagnostics;

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
            // string localAgentConfigFilePath = _fileSystem.Path.GetTempFilePath();
            // var content = JsonHelpers.SerializeObject(config);
            // _fileSystem.WriteAllTextToFile(localAgentConfigFilePath, content);

            // var commandLine = new StringBuilder();
            // commandLine.Append($"run --privileged -dit --name {_localAgentContainerName} ");

            // // Add the file mount for the LocalAgent config
            // commandLine.Append($"-v \"{localAgentConfigFilePath}:{Common.Constants.LocalAgent.LocalAgentConfigPath}\" ");

            // // Add the file mount for the kubeConfig
            // commandLine.Append($"-v \"{kubeConfigDetails.Path}:{Common.Constants.LocalAgent.KubeConfigPath}\" ");
            // commandLine.Append($"--env \"KUBECONFIG={Common.Constants.LocalAgent.KubeConfigPath}\" ");

            // // Add the NET_ADMIN privileges: this is used to run iptables
            // commandLine.Append($"--cap-add=NET_ADMIN ");

            // // expose a PORT for the local agent to listen on
            // commandLine.Append($"--network={config.NetworkName} ");

            // // Create --add-host arguments
            // foreach (var endpoint in config.ReachableEndpoints)
            // {
            //     endpoint.ValidateDnsName();
            //     var serviceAliases = endpoint.GetServiceAliases(config.RemoteAgentInfo.NamespaceName, this._log);
            //     foreach (var serviceAlias in serviceAliases)
            //     {
            //         commandLine.Append($"--add-host \"{serviceAlias}:{endpoint.LocalIP}\" ");
            //     }
            // }
            // // add /etc/host as volume
            // // commandLine.Append("-v /etc/hosts:/etc/hosts ");
            // // local agent image
            // commandLine.Append("hsubramanian/localagent:marinerv4");

            // this.RunDockerCommand(commandLine.ToString());
            var yamlstring = createDockerComposeFile(config, kubeConfigDetails);
            string dockerComposeFilePath = _fileSystem.Path.GetTempFilePath(Guid.NewGuid().ToString("N") + ".yml");
            _fileSystem.WriteAllTextToFile(dockerComposeFilePath, yamlstring);
            _log.Verbose("docker compose temp file path: "+ dockerComposeFilePath);
            AssertHelper.True(_fileSystem.FileExists(dockerComposeFilePath), $"docker compose file is missing: '{dockerComposeFilePath}'");
            var commandLine = new StringBuilder();
            /*string filePath = _fileSystem.Path.Combine("Files", "docker-compose.yml");
            AssertHelper.True(_fileSystem.FileExists(filePath), $"docker compose file is missing: '{filePath}'");*/
            commandLine.Append("-p localagent ");
            commandLine.Append($"-f \"{dockerComposeFilePath}\" ");
            commandLine.Append("up -d");
            this.RunDockerComposeUpCommand(commandLine.ToString());
        }

        private string createDockerComposeFile(LocalAgentConfig config, KubeConfigDetails kubeConfigDetails)
        {
            string localAgentConfigFilePath = _fileSystem.Path.GetTempFilePath();
            var content = JsonHelpers.SerializeObject(config);
            _fileSystem.WriteAllTextToFile(localAgentConfigFilePath, content);
            var configPathForVolume = localAgentConfigFilePath + ":" + LocalAgent.LocalAgentConfigPath;
            var kubeconfigPathForVolume = kubeConfigDetails.Path + ":" + LocalAgent.KubeConfigPath;
            var localSourceCodeMount = config.LocalSourceCodePath + ":" + LocalAgent.LocalSourceCodePath;

            DockerCompose dockerCompose = new DockerCompose();
            Services services = new Services
            {
                // user workload container
                Devcontainer = new DevContainer(),
            };
            // local agent container
            services.Localagent = new LocalAgentContainer
            {
                ContainerName = _localAgentContainerName + "-localagent",
                Image = "docker.io/hsubramanian/localagent:marinerv4",
                Volumes = new List<string>(),
                Environment = new List<string>(),
                CapAdd = new List<string>()
            };
            services.Localagent.Volumes.Add(configPathForVolume);
            services.Localagent.Volumes.Add(kubeconfigPathForVolume);
            services.Localagent.Environment.Add($"KUBECONFIG={LocalAgent.KubeConfigPath}");
            services.Localagent.CapAdd.Add("NET_ADMIN");
            services.Localagent.ExtraHosts = frameExtraHosts(config);
            services.Localagent.Healthcheck = new HealthCheck
            {
                Test = new string[4] { "CMD", "curl", "-f", "http://localhost:7891/health"},
                Interval = "2s",
                Timeout = "60s",
                Retries = 3
            };
            // user workload container
            services.Devcontainer.Image = config.UserWorkloadImageName;
            services.Devcontainer.Volumes = new List<string>
            {
                localSourceCodeMount
            };
            services.Devcontainer.Environment = new List<string>
            {
                $"PORT={config.ReversePortForwardInfo.First(a => a.LocalPort != null).LocalPort}"
            };
            services.Devcontainer.Environment.AddRange(appendUserServiceHost(config.EnvironmentVariables));
            services.Devcontainer.ContainerName = _localAgentContainerName;
            // possible values always, on_failure
            services.Devcontainer.Restart = "always";
            services.Devcontainer.DependsOn = new DependsOn();
            services.Devcontainer.DependsOn.DependsOnName = new DependsOnName();
            // possible values service_started, service_healthy, service_completed_successfully
            services.Devcontainer.DependsOn.DependsOnName.Condition = "service_healthy";
            services.Devcontainer.DependsOn.DependsOnName.Restart = true;
            services.Devcontainer.NetworkMode = "service:localagent"; // this will spin up user workload in same network as local agent      
            dockerCompose.Services = services;
            // serialize
            var serializer = new SerializerBuilder()
                .Build();
            var yaml = serializer.Serialize(dockerCompose);
            return yaml;
        }

        private IEnumerable<string> appendUserServiceHost(IDictionary<string, string> envVars)
        {
            IList<string> envVariables = new List<string>();
            foreach (var key in envVars.Keys)
            {

                envVariables.Add($"{key}={envVars[key]}");
            }
            return envVariables;
        }

        private List<string> frameExtraHosts(LocalAgentConfig config)
        {
            var hosts = new List<string>();
            foreach (var endpoint in config.ReachableEndpoints)
            {
                endpoint.ValidateDnsName();
                var serviceAliases = endpoint.GetServiceAliases(config.RemoteAgentInfo.NamespaceName, this._log);
                foreach (var serviceAlias in serviceAliases)
                {
                    hosts.Add($"{serviceAlias}:{endpoint.LocalIP}");
                }
            }

            return hosts;
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