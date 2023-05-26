using Microsoft.Extensions.Diagnostics.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.Models.Docker
{
    internal class DockerCompose
    {

        [YamlMember(Alias = "services")]
        public Services Services { get; set; }
    }

    public partial class Services
    {
        [YamlMember(Alias = "devcontainer")]
        public DevContainer Devcontainer { get; set; }

        [YamlMember(Alias = "localagent")]
        public LocalAgentContainer Localagent { get; set; }
    }

    public partial class DevContainer
    {
        [YamlMember(Alias = "container_name")]
        public string ContainerName { get; set; }

        [YamlMember(Alias = "image")]
        public string Image { get; set; }

        [YamlMember(Alias = "network_mode")]
        public string NetworkMode { get; set; }

        [YamlMember(Alias = "volumes")]
        public List<string> Volumes { get; set; }

        [YamlMember(Alias = "environment")]
        public List<string> Environment { get; set; }

        [YamlMember(Alias = "restart")]
        public string Restart { get; set; }

        [YamlMember(Alias = "depends_on")]
        public DependsOn DependsOn { get; set; }

    }

    public partial class DependsOn
    {
        [YamlMember(Alias = "localagent")]
        public DependsOnName DependsOnName;
    }

    public partial class DependsOnName
    {
        [YamlMember(Alias = "condition")]
        public string Condition { get; set; }

        [YamlMember(Alias = "restart")]
        public bool Restart { get; set; }
    }

    public partial class LocalAgentContainer
    {
        [YamlMember(Alias = "container_name")]
        public string ContainerName { get; set; }

        [YamlMember(Alias = "image")]
        public string Image { get; set; }

        [YamlMember(Alias = "volumes")]
        public List<string> Volumes { get; set; }


        [YamlMember(Alias = "environment")]
        public List<string> Environment { get; set; }

        [YamlMember(Alias = "cap_add")]
        public List<string> CapAdd { get; set; }

        [YamlMember(Alias = "extra_hosts")]
        public List<string> ExtraHosts { get; set; }

        [YamlMember(Alias = "healthcheck")]
        public HealthCheck Healthcheck { get; set; }

    }

    public partial class HealthCheck
    {
        [YamlMember(Alias = "test")]
        public string[] Test { get; set; }

        [YamlMember(Alias = "interval")]
        public string Interval { get; set; }

        [YamlMember(Alias = "timeout")]
        public string Timeout { get; set; }

        [YamlMember(Alias = "retries")]
        public long Retries { get; set; }
    }
}
