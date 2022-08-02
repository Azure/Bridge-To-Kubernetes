// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class ReleaseUtilities
    {
        public static string GenerateReleaseName(string spaceName, string chartName)
        {
            var releaseNameHash = $"{spaceName}${chartName}".Sha256Hash(6);
            var releaseName = $"{Constants.Product.NameAbbreviation}-{releaseNameHash}-{spaceName}-{chartName}"; // lpk-123456-space-service
            return releaseName.Substring(0, Math.Min(43, releaseName.Length));
        }

        /// <summary>
        /// Get chart name from release name
        /// </summary>
        /// <param name="releaseName">This will be of the form lpk-22b1cc-default-devsite</param>
        public static string GetChartNameFromReleaseName(string releaseName)
        {
            if (string.IsNullOrEmpty(releaseName))
            {
                return string.Empty;
            }
            var releaseNameParts = releaseName.Split('-');
            return releaseNameParts.Last();
        }
    }
}