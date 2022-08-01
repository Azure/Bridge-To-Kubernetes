// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    public interface IServiceToken : IEnvironmentToken
    {
        int[] Ports { get; set; }

        string IpAddress { get; set; }
    }
}