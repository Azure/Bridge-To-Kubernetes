// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.LocalAgentManagement
{
    public interface ILocalAgentManager
    {
        /// <summary>
        /// Start the containerized LocalAgent
        /// </summary>
        void StartLocalAgent(LocalAgentConfig config, KubeConfigDetails kubeConfigDetails);

        /// <summary>
        /// Stop the containerized LocalAgent
        /// </summary>
        void StopLocalAgent();
    }
}