// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.DevHost
{
    /// <summary>
    /// Object model for devhostagent's "connectedsessions" API
    /// </summary>
    public class ConnectedSessionsResponseModel
    {
        /// <summary>
        /// The number of connected clients
        /// </summary>
        [JsonPropertyName("numConnectedSessions")]
        public int NumConnectedSessions { get; set; }
    }
}