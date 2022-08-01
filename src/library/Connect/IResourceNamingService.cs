// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Models;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Naming local resources.
    /// </summary>
    internal interface IResourceNamingService
    {
        string GetVolumeName(string name);

        string GetPodContainerName();

        string GetServiceRouterName(EndpointInfo endpointInfo);

        string GetWorkloadContainerName();
    }
}