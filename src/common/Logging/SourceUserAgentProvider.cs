// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class SourceUserAgentProvider
    {
        private readonly Lazy<string> _userAgent;

        // If it exists, gets the source user agent transmitted by the caller as an environment variable, otherwise use the application name
        public string UserAgent => _userAgent.Value;

        public SourceUserAgentProvider(IEnvironmentVariables environmentVariables, IAssemblyMetadataProvider assemblyUtility, string applicationName)
        {
            _userAgent = new Lazy<string>(() =>
            {
                var userAgent = environmentVariables.SourceUserAgent;
                if (string.IsNullOrEmpty(userAgent))
                {
                    // No source user agent has been defined so use applicationName/version
                    userAgent = $"{applicationName}/{assemblyUtility.AssemblyVersion}";
                }
                return userAgent;
            });
        }
    }
}