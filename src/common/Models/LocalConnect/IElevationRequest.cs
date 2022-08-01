// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Describes the context for a process needing admin privileges
    /// </summary>
    public interface IElevationRequest
    {
        /// <summary>
        /// Type of elevation request
        /// </summary>
        ElevationRequestType RequestType { get; }

        /// <summary>
        /// Returns what the request means in human-readable form
        /// </summary>
        string ConvertToReadableString();
    }
}