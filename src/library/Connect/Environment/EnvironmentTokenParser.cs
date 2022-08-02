// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static Microsoft.BridgeToKubernetes.Library.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    /// <summary>
    /// Class used to parse token in the $() format e.g. $(volumes.product-cert)
    /// </summary>
    internal static class EnvironmentTokenParser
    {
        public static EnvironmentTokenBase Parse(string tokenStr, bool isReplacementToken, LocalProcessConfigFile config)
        {
            if (!isReplacementToken)
            {
                return new VerbatimToken(tokenStr);
            }

            if (tokenStr == null || tokenStr.Length <= 3 || tokenStr[0] != '$' || tokenStr[1] != '(' || tokenStr[tokenStr.Length - 1] != ')')
            {
                throw new ArgumentException($"Token '{tokenStr}' must be of format $(..)");
            }
            string token = tokenStr.Substring(2, tokenStr.Length - 3); // unwrap the token from $()

            // TODO: implement real file token, in the documentation we talk about the possiblity of mounting files without specifying volumes but this doesn't actually work today

            // Parse for volumes, services and external endpoints tokens.
            int i = token.IndexOf(':');
            if (i < 0)
            {
                throw new ArgumentException($"Unknown token in '{tokenStr}'");
            }

            // Volume tokens start with "volumeMounts:" while service tokens start with "services:", and external endpoint tokens start with "externalEndpoints:".
            var tokenType = token.Substring(0, i);
            string leftOver = token.Substring(i + 1);

            if (string.IsNullOrWhiteSpace(leftOver))
            {
                throw new ArgumentException($"Unknown token in '{tokenStr}'");
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(tokenType, Config.Tokens.VolumeMounts))
            {
                return ParseVolumeToken(leftOver, config);
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(tokenType, Config.Tokens.Services))
            {
                return ParseServiceToken(leftOver);
            }
            else if (StringComparer.OrdinalIgnoreCase.Equals(tokenType, Config.Tokens.ExternalEndpoints))
            {
                return ParseExternalEndpointToken(leftOver);
            }
            else
            {
                throw new ArgumentException($"Unknown token type '{tokenType}' in '{tokenStr}'");
            }
        }

        /// <summary>
        /// In case of Volume tokens the user can specify additional configuration options in the volumeMounts section.
        /// If not specified we are going to create a random temp folder to download the volume
        /// </summary>
        /// <param name="volumeName"></param>
        /// <returns></returns>
        private static VolumeToken ParseVolumeToken(string volumeName, LocalProcessConfigFile config)
        {
            var volumeMountEntry = config.VolumeMounts?.FirstOrDefault(v => v.Name.EqualsIgnoreCase(volumeName));
            string localPath = string.Empty;
            if (!string.IsNullOrWhiteSpace(volumeMountEntry?.LocalPath) && IsValidPath(volumeMountEntry.LocalPath, volumeName))
            {
                localPath = volumeMountEntry.LocalPath;
            }

            return new VolumeToken(name: volumeName, tokenString: volumeName, localPath: localPath);
        }

        private static ServiceToken ParseServiceToken(string token)
        {
            string[] tokens = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 2)
            {
                throw new ArgumentException(string.Format(Resources.InvalidServiceEntryFormat, token, Config.FilePath));
            }

            string[] serviceNameTokens = tokens[0].Split('.');
            if (serviceNameTokens.Length > 2)
            {
                throw new ArgumentException(string.Format(Resources.InvalidServiceEntryFormat, tokens[0], Config.FilePath));
            }
            return new ServiceToken(name: tokens[0], tokenString: token, ports: tokens.Length == 1 ? new int[] { } : GetPortsArray(tokens[1]));
        }

        private static ExternalEndpointToken ParseExternalEndpointToken(string token)
        {
            string[] tokens = token.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length != 2)
            {
                throw new ArgumentException(string.Format(Resources.InvalidExternalEndpointEntryFormat, token, Config.FilePath));
            }
            return new ExternalEndpointToken(name: tokens[0], tokenString: token, ports: GetPortsArray(tokens[1]));
        }

        private static int[] GetPortsArray(string portString)
        {
            var result = new List<int>();
            string[] portTokens = portString.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var pt in portTokens)
            {
                if (!int.TryParse(pt, out int p) || p <= 0 || p >= 65536)
                {
                    throw new ArgumentException(string.Format(Resources.InvalidPortEntryFormat, portString, Config.FilePath));
                }
                result.Add(p);
            }
            return result.ToArray();
        }

        private static bool IsValidPath(string path, string entryName)
        {
            if (path.Length > 255)
            {
                throw new ArgumentException($"Invalid path specified for VolumeMount {entryName}: File path is too long");
            }

            var invalidPathChars = Path.GetInvalidPathChars();
            foreach (var c in invalidPathChars)
            {
                if (path.Contains(c))
                {
                    throw new ArgumentException($"Invalid path specified for VolumeMount {entryName}: '{path}'");
                }
            }

            return true;
        }
    }
}