// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.Models.Settings;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    /// <summary>
    /// <see cref="IKubernetesPortForwardManager"/> talks to the kube API server to forward a pods port to the localhost
    /// </summary>
    internal interface IKubernetesPortForwardManager
    {
        /// <summary>
        /// Starts a port forward from a pod to localhost
        /// </summary>
        void StartContainerPortForward(
            string namespaceName,
            string podName,
            int localPort,
            int remotePort,
            Action<PortPair> onSuccessfulPortForward = null,
            CancellationToken cancellationToken = default(CancellationToken));
    }
}