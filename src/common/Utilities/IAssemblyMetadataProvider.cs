// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// This utility and the <see cref="IAssemblyMetadataProvider"/> interface provide abstraction of <see cref="AssemblyVersionUtilities"/> to enable unit testing
    /// </summary>
    internal interface IAssemblyMetadataProvider
    {
        string AssemblyVersion { get; }
    }
}