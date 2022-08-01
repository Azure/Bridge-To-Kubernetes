// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Library.Utilities
{
    internal static class EmbeddedFileUtilities
    {
        /// <summary>
        /// Retrieves an image tag from the imagetag.setting file embedded in this assembly that matches the given linePrefix
        /// </summary>
        public static string GetImageTag(string linePrefix)
        {
            var platform = new Platform();
            var fileSystem = new FileSystem(new EnvironmentVariables(platform), platform, new PathUtilities(platform));
            string imagetagFile = AsyncHelpers.RunSync(() => fileSystem.ReadFileFromEmbeddedResourceInAssemblyAsync(Assembly.GetExecutingAssembly(), "imagetag.setting"));
            // Ex. LINE_PREFIX=<tag>
            string imageTagLine = imagetagFile.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(line => line.Trim())
                                              .Where(line => line.StartsWith(linePrefix))
                                              .Single();
            string tag = imageTagLine.Substring(linePrefix.Length + 1);
            return tag;
        }
    }
}