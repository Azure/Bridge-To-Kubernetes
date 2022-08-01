// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.LocalAgent
{
    public class NetworkDetails
    {
        public string RemoteIP { get; set; }
        public string LocalIP { get; set; }
        public int RemotePort { get; set; }
        public int LocalPort { get; set; }
        public string Host { get; set; }
    }
}