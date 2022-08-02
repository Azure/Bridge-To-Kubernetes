// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// A request for elevated permissions in order to edit the hosts file on the machine
    /// </summary>
    public interface IEditHostsFileRequest : IElevationRequest
    {
    }
}