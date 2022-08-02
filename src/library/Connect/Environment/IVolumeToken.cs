// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    public interface IVolumeToken : IEnvironmentToken
    {
        string LocalPath { get; set; }
    }
}