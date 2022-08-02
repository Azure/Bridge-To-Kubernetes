// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    /// <summary>
    /// Kube config file management client
    /// </summary>
    public interface IKubeConfigManagementClient : IDisposable
    {
        /// <summary>
        /// Get the complete KubeConfigDetails of the active KubeConfig
        /// </summary>
        public KubeConfigDetails GetKubeConfigDetails();
    }
}