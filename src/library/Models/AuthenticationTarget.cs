// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    public class AuthenticationTarget
    {
        [JsonPropertyName("Url")]
        public string Url { get; set; }

        [JsonPropertyName("AuthenticationCode")]
        public string AuthenticationCode { get; set; }
    }
}