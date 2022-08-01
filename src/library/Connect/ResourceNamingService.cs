// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.BridgeToKubernetes.Common.Models;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// Implements IResourceNamingService.
    /// </summary>
    internal class ResourceNamingService : IResourceNamingService
    {
        private string _seed;

        public ResourceNamingService(string seed = null)
        {
            _seed = string.IsNullOrEmpty(seed) ? Process.GetCurrentProcess().Id.ToString() : seed;
        }

        public string GetVolumeName(string name)
        {
            return $"{_seed}-{name}";
        }

        public string GetPodContainerName()
        {
            return $"{_seed}-pod";
        }

        public string GetServiceRouterName(EndpointInfo endpointInfo)
        {
            return $"{_seed}-router-{endpointInfo.DnsName}";
        }

        public string GetWorkloadContainerName()
        {
            return _seed;
        }
    }
}