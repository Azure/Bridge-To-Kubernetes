// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class HashHelpers
    {
        internal static int GetCombinedHash(int hash1, int hash2)
        {
            // Bernstein Hash(djb2)
            // From System.Web.Util.HashCodeCombiner
            // Reference: http://referencesource.microsoft.com/#mscorlib/system/array.cs,87d117c8cc772cca
            // hash = hash1 * 33 ^ hash2
            return (((hash1 << 5) + hash1) ^ hash2);
        }

        internal static int GetCombinedHash(string s1, string s2)
        {
            int primeHash = 5381;
            return GetCombinedHash(GetCombinedHash(primeHash, s1.GetHashCode()), s2.GetHashCode());
        }
    }
}