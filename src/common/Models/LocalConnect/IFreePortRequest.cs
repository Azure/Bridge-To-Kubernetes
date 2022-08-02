// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// An elevated access request for freeing ports
    /// </summary>
    public interface IFreePortRequest : IElevationRequest
    {
        /// <summary>
        /// List of occupied ports to release
        /// </summary>
        IList<IPortMapping> OccupiedPorts { get; }
    }
}