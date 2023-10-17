// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.Library.Utilities;

namespace Microsoft.BridgeToKubernetes.Library
{
    internal class ImageProvider : IImageProvider
    {
        private readonly ILog _log;
        private readonly IEnvironmentVariables _environmentVariables;

        private readonly IKubernetesManagementClient _kubernetesManagementClient;

        private AsyncLazy<string> _devHostImage;
        private AsyncLazy<string> _devHostRestorationJobImage;
        private AsyncLazy<string> _routingManagerImage;

        private static readonly IReadOnlyDictionary<ReleaseEnvironment, string> ContainerRegistries = new Dictionary<ReleaseEnvironment, string>()
            {
                { ReleaseEnvironment.Production, "bridgetokubernetes.azurecr.io" },
                { ReleaseEnvironment.Staging, "mindarostage.azurecr.io" },
                { ReleaseEnvironment.Development, "mindarodev.azurecr.io" },
                { ReleaseEnvironment.Local, "mindarodev.azurecr.io" },
                { ReleaseEnvironment.Test, "mindarodev.azurecr.io" }
            };

        internal static class DevHost
        {
            // To change DevHostImageName tag, please update deployment\settings\services\imagetag.setting accordingly
            // During development, use environment variable LPK_DEVHOSTIMAGENAME to override the default devhostAgent image name

            private static Lazy<string> _tag = new(() => EmbeddedFileUtilities.GetImageTag("MINDARO_DEVHOSTAGENT_TAG"));
            public static string Version => _tag.Value;

            public static string Name => Common.Constants.ImageName.RemoteAgentImageName;

            /// <summary>
            /// These entrypoints are supported by the devhostAgent image - they all point to the same start entrypoint.
            /// </summary>
            public static readonly IEnumerable<string> SupportedEntryPoints = new string[]
            {
                "dotnet",
                "/usr/bin/dotnet",
                "/usr/local/bin/dotnet",
                "npm",
                "/usr/bin/npm",
                "node",
                "/usr/bin/node",
                "/entrypoint.sh",
                "entrypoint.sh"
            };
        }

        internal static class DevHostRestorationJob
        {
            // To change RestorationJobImageName tag, please update deployment\settings\services\imagetag.setting accordingly
            // During development, use environment variable LPK_RESTORATIONJOBIMAGENAME to override the default restorationjob image name
            private static Lazy<string> _tag = new(() => EmbeddedFileUtilities.GetImageTag("MINDARO_DEVHOSTAGENT_RESTORATIONJOB_TAG"));

            public static string Version => _tag.Value;

            internal static string Name => Common.Constants.ImageName.RestorationJobImageName;
        }

        private static class RoutingManager
        {
            public static string Version => Common.Constants.ImageTag.RoutingManagerImageTag;
            internal static string Name => Common.Constants.Routing.RoutingManagerNameLower;
        }

        public ImageProvider(ILog log, IEnvironmentVariables environmentVariables, IManagementClientFactory managementClientFactory)
        {
            _log = log;
            _environmentVariables = environmentVariables;
            var kubeConfigManagementClient = managementClientFactory.CreateKubeConfigClient();
            var kubeConfigDetails = kubeConfigManagementClient.GetKubeConfigDetails();
            _kubernetesManagementClient = managementClientFactory.CreateKubernetesManagementClient(kubeConfigDetails);
            _devHostImage = new AsyncLazy<string>(async () => await GetImage(_environmentVariables.DevHostImageName, DevHost.Name, DevHost.Version));
            _devHostRestorationJobImage = new AsyncLazy<string>(async () => await GetImage(_environmentVariables.DevHostRestorationJobImageName, DevHostRestorationJob.Name, DevHostRestorationJob.Version));
            _routingManagerImage = new AsyncLazy<string>(async () => await GetImage(_environmentVariables.RoutingManagerImageName, RoutingManager.Name, RoutingManager.Version));
        }

        public string DevHostImage => _devHostImage.GetAwaiter().GetResult();
        public string DevHostRestorationJobImage => _devHostRestorationJobImage.GetAwaiter().GetResult();
        public string RoutingManagerImage => _routingManagerImage.GetAwaiter().GetResult();

        public async Task<string> GetImage(string overrideImage, string defaultImage, string tag)
        {
            if (!string.IsNullOrWhiteSpace(overrideImage))
            {
                _log.Warning($"Overriding default image '{defaultImage}' with '{overrideImage}'");
                return overrideImage;
            }

            // determine if nodes are running arm architecture
            V1NodeList nodes = (await _kubernetesManagementClient.ListNodes(new CancellationToken())).Value;
            // this can never happen or timed out while getting nodes
            if (nodes?.Items == null || !nodes.Items.Any())
            {
                _log.Warning($"No nodes found in cluster. Using default image '{defaultImage}' with '{tag}'");
                return $"{ContainerRegistries[_environmentVariables.ReleaseEnvironment]}/{defaultImage}:{tag}";
            }
            bool isAllNodesArmArch = nodes.Items.All(node => node?.Status?.NodeInfo?.Architecture == Common.Constants.Architecture.Arm64);
            
            if (isAllNodesArmArch)
            {
                return $"{ContainerRegistries[_environmentVariables.ReleaseEnvironment]}/{defaultImage}-arm64:{tag}";
            }

            return $"{ContainerRegistries[_environmentVariables.ReleaseEnvironment]}/{defaultImage}:{tag}";
        }
    }
}