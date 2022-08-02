// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal class ApiVersionHelper
    {
        /// <summary>
        /// Compares two API versions.
        /// </summary>
        /// <param name="apiVersion">The first version to be compared.</param>
        /// <param name="otherVersion">The other version to compare to.</param>
        /// <returns>A positive integer if <c>apiVersion</c> is newer than <c>otherVersion</c>,
        /// 0 of they are equal, or a negative number if <c>apiVersion</c> is older than <c>otherVersion</c>.</returns>
        public static int CompareVersions(string apiVersion, string otherVersion)
        {
            if (string.IsNullOrWhiteSpace(apiVersion))
            {
                throw new ArgumentException("A version to compare to is required", nameof(apiVersion));
            }
            if (string.IsNullOrWhiteSpace(otherVersion))
            {
                throw new ArgumentException("A version to compare to is required", nameof(otherVersion));
            }
            GetParsedVersion(otherVersion, out var otherVersionMajor, out var otherVersionMinor);
            GetParsedVersion(apiVersion, out var versionMajor, out var versionMinor);
            if (versionMajor == otherVersionMajor)
            {
                return (versionMinor - otherVersionMinor);
            }
            else
            {
                return (versionMajor - otherVersionMajor);
            }
        }

        /// <summary>
        /// Parses a version string into it's major and minor components.
        /// </summary>
        /// <param name="version">The version string to be parsed.</param>
        /// <param name="versionMajor">The major version.</param>
        /// <param name="versionMinor">The minor version.</param>
        public static void GetParsedVersion(string version, out int versionMajor, out int versionMinor)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ArgumentNullException($"Version to be parsed can't be null or empty");
            }

            var versionSplit = version.Split('.');
            bool validInput = versionSplit.Length <= 2;

            versionMajor = 0;
            versionMinor = 0;
            validInput = validInput && int.TryParse(versionSplit[0], out versionMajor);
            validInput = validInput && (versionSplit.Length == 1 || int.TryParse(versionSplit[1], out versionMinor));
            if (!validInput)
            {
                throw new ArgumentException($"{nameof(version)} should be of the form \\d[.\\d]");
            }
        }
    }
}