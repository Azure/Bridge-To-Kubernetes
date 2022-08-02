// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Utilities;

namespace Microsoft.BridgeToKubernetes.Library
{
    internal class ImageProvider : IImageProvider
    {
        private readonly ILog _log;
        private readonly IEnvironmentVariables _environmentVariables;

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
            private static Lazy<string> _name = new Lazy<string>(() =>
            {
                string tag = EmbeddedFileUtilities.GetImageTag("MINDARO_DEVHOSTAGENT_TAG");
                return $"{Common.Constants.ImageName.RemoteAgentImageName}:{tag}";
            });

            public static string Name => _name.Value;

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
            private static Lazy<string> _tag = new Lazy<string>(() => EmbeddedFileUtilities.GetImageTag("MINDARO_DEVHOSTAGENT_RESTORATIONJOB_TAG"));

            public static string Version => _tag.Value;

            internal static string Name => $"lpkrestorationjob:{_tag.Value}";
        }

        private static class RoutingManager
        {
            internal static string Name => $"{Common.Constants.Routing.RoutingManagerNameLower}:stable";
        }

        public ImageProvider(ILog log, IEnvironmentVariables environmentVariables)
        {
            _log = log;
            _environmentVariables = environmentVariables;

            _devHostImage = new Lazy<string>(() => GetImage(_environmentVariables.DevHostImageName, DevHost.Name));
            _devHostRestorationJobImage = new Lazy<string>(() => GetImage(_environmentVariables.DevHostRestorationJobImageName, DevHostRestorationJob.Name));
            _routingManagerImage = new Lazy<string>(() => GetImage(_environmentVariables.RoutingManagerImageName, RoutingManager.Name));
        }

        public string DevHostImage => _devHostImage.Value;
        public string DevHostRestorationJobImage => _devHostRestorationJobImage.Value;
        public string RoutingManagerImage => _routingManagerImage.Value;

        private string GetImage(string overrideImage, string defaultImage)
        {
            if (!string.IsNullOrWhiteSpace(overrideImage))
            {
                _log.Warning($"Overriding default image '{defaultImage}' with '{overrideImage}'");
                return overrideImage;
            }

            return $"{ContainerRegistries[_environmentVariables.ReleaseEnvironment]}/{defaultImage}";
        }
    }
}