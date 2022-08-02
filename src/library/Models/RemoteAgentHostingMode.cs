// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// Different modes of deploying devhostagent on the remote environment
    /// </summary>
    internal enum RemoteAgentHostingMode
    {
        NewPod,
        NewPodWithContext,
        Replace,
        PrepConnect
    }
}