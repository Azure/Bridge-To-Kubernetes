// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    public interface IExternalEndpointToken : IEnvironmentToken
    {
        int[] Ports { get; set; }
    }
}