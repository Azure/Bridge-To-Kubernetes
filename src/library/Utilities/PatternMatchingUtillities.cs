// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.BridgeToKubernetes.Library.Utilities
{
    public static class PatternMatchingUtillities
    {
        /// <summary>
        /// Checks if a input value matches a given wildcard pattern, ie "default-token-*"
        /// </summary>
        public static bool IsMatch(string inputPattern, string input)
        {
            if (!inputPattern.Contains('*'))
            {
                return input.Equals(inputPattern, StringComparison.CurrentCultureIgnoreCase);
            }

            string regexPattern = string.Concat("^", Regex.Escape(inputPattern).Replace("\\*", ".*?"), "$");

            return Regex.IsMatch(input, regexPattern, RegexOptions.IgnoreCase);
        }
    }
}
