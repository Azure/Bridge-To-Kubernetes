// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Newtonsoft.Json;

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
        [JsonProperty("numConnectedSessions")]
        public int NumConnectedSessions { get; set; }
    }
}