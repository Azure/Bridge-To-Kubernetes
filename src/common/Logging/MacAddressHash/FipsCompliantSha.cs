// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Security.Cryptography;

namespace Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash
{
    // Used to hash the MacAddress in the same way VS does to get the same DeviceId
    internal class FipsCompliantSha
    {
        // FIPS compliant SHA256 hash algorithm.
        public static readonly SHA256 Sha256 = SHA256.Create();
    }
}