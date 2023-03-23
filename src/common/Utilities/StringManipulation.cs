// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text.RegularExpressions;


namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class StringManipulation
    {
        public static string RemovePrivateKeyIfNeeded(string inputString)
        {
            if (inputString.ContainsIgnoreCase("BEGIN PRIVATE KEY"))
            {
                return Regex.Replace(inputString, @"(BEGIN PRIVATE KEY(\s|.)*END PRIVATE KEY)", "KEY_WAS_REMOVED");
            }
            return inputString;
        }
    }
}