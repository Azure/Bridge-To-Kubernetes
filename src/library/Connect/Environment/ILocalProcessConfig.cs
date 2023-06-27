// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal interface ILocalProcessConfig
    {
        /// <summary>
        /// The file path of the local process config file that was loaded
        /// </summary>
        string ConfigFilePath { get; }

        /// <summary>
        /// Whether there are any error-level issues in the loaded file
        /// </summary>
        bool IsSuccess { get; }

        /// <summary>
        /// All issues loading the file
        /// </summary>
        IEnumerable<EnvironmentEntryIssue> AllIssues { get; }

        /// <summary>
        /// Error-level issues loading the file
        /// </summary>
        IEnumerable<EnvironmentEntryIssue> ErrorIssues { get; }

        /// <summary>
        /// List of services referenced by the local env file
        /// </summary>
        IEnumerable<IServiceToken> ReferencedServices { get; }

        /// <summary>
        /// List of volumes referenced by the local env file
        /// </summary>
        IEnumerable<IVolumeToken> ReferencedVolumes { get; }

        /// <summary>
        /// List of external endpoints referenced by the local env file
        /// </summary>
        IEnumerable<IExternalEndpointToken> ReferencedExternalEndpoints { get; }

        /// <summary>
        /// Is Managed identity one of the enabled features
        /// </summary>
        bool IsManagedIdentityScenario { get; }

        /// <summary>
        /// Is Probes one of the enabled features
        /// </summary>
        bool IsProbesEnabled { get; }

        /// <summary>
        /// Is Container Lifecycle Hooks one of the enabled features
        /// </summary>
        bool IsLifecycleHooksEnabled { get; }

        /// <summary>
        /// Returns a collection with the environment variables generated from the local env file
        /// </summary>
        /// <returns></returns>
        IDictionary<string, string> EvaluateEnvVars();
    }
}