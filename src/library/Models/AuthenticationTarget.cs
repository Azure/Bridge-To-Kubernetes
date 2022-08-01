// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Newtonsoft.Json;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    public class AuthenticationTarget
    {
        [JsonProperty("Url")]
        public string Url { get; set; }

        [JsonProperty("AuthenticationCode")]
        public string AuthenticationCode { get; set; }
    }
}