// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Describes the context for a process needing admin privileges
    /// </summary>
    public abstract class ElevationRequest : IElevationRequest
    {
        /// <summary>
        /// <see cref="IElevationRequest.RequestType"/>
        /// </summary>
        public abstract ElevationRequestType RequestType { get; }

        /// <summary>
        /// <see cref="IElevationRequest.ConvertToReadableString"/>
        /// </summary>
        public abstract string ConvertToReadableString();
    }
}