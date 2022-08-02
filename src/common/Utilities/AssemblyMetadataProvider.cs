// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// <see cref="IAssemblyMetadataProvider"/>
    /// </summary>
    internal class AssemblyMetadataProvider : IAssemblyMetadataProvider
    {
        private Lazy<string> _assemblyVersion = new Lazy<string>(() =>
        {
            return AssemblyVersionUtilities.GetEntryAssemblyInformationalVersion();
        });

        public string AssemblyVersion => _assemblyVersion.Value;
    }
}