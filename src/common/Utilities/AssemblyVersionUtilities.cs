// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class AssemblyVersionUtilities
    {
        /// <summary>
        /// Retrieves the informational version from the assembly that started current the execution.
        /// </summary>
        public static string GetEntryAssemblyInformationalVersion()
        {
            AssemblyInformationalVersionAttribute attribute = (AssemblyInformationalVersionAttribute)Assembly.GetEntryAssembly()?.GetCustomAttributes().Where(a => a is AssemblyInformationalVersionAttribute).FirstOrDefault();
            return attribute?.InformationalVersion;
        }

        /// <summary>
        /// Retrieves the informational version from the assembly that called this method.
        /// </summary>
        public static string GetCallingAssemblyInformationalVersion()
        {
            AssemblyInformationalVersionAttribute attribute = (AssemblyInformationalVersionAttribute)Assembly.GetCallingAssembly()?.GetCustomAttributes().Where(a => a is AssemblyInformationalVersionAttribute).FirstOrDefault();
            return attribute?.InformationalVersion;
        }
    }
}