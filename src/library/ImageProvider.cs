// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Utilities;

namespace Microsoft.BridgeToKubernetes.Library
{
    internal class ImageProvider : IImageProvider
    {
        private readonly ILog _log;
        private readonly IEnvironmentVariables _environmentVariables;

        private readonly IKubernetesClient _kubernetesClient;

        private Lazy<string> _devHostImage;
        private Lazy<string> _devHostRestorationJobImage;
        private Lazy<string> _routingManagerImage;

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

        public ImageProvider(ILog log, IEnvironmentVariables environmentVariables, IKubernetesClient kubernetesClient)
        {
            _log = log;
            _environmentVariables = environmentVariables;
            _kubernetesClient = kubernetesClient;

            // determine if nodes are running arm architecture
            Task<V1NodeList> nodes = _kubernetesClient.ListNodes();
            nodes.Wait();
            bool isAllNodesArmArch = nodes.Result.Items.All(node => node?.Status?.NodeInfo?.Architecture == Common.Constants.Architecture.Arm64);


            _devHostImage = new Lazy<string>(() => GetImage(_environmentVariables.DevHostImageName, DevHost.Name, DevHost.Version, isAllNodesArmArch));
            _devHostRestorationJobImage = new Lazy<string>(() => GetImage(_environmentVariables.DevHostRestorationJobImageName, DevHostRestorationJob.Name, DevHostRestorationJob.Version, isAllNodesArmArch));
            _routingManagerImage = new Lazy<string>(() => GetImage(_environmentVariables.RoutingManagerImageName, RoutingManager.Name, RoutingManager.Version ,isAllNodesArmArch));
        }

        public string DevHostImage => _devHostImage.Value;
        public string DevHostRestorationJobImage => _devHostRestorationJobImage.Value;
        public string RoutingManagerImage => _routingManagerImage.Value;

        private string GetImage(string overrideImage, string defaultImage, string tag, bool isAllNodesArmArch)
        {
            if (!string.IsNullOrWhiteSpace(overrideImage))
            {
                _log.Warning($"Overriding default image '{defaultImage}' with '{overrideImage}'");
                return overrideImage;
            }

            if (isAllNodesArmArch)
            {
                return $"{ContainerRegistries[_environmentVariables.ReleaseEnvironment]}/{defaultImage}-arm64:{tag}";
            }

            return $"{ContainerRegistries[_environmentVariables.ReleaseEnvironment]}/{defaultImage}:{tag}";
        }
    }
}