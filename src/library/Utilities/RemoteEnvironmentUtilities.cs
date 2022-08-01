// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Library.Utilities
{
    internal static class RemoteEnvironmentUtilities
    {
        private static Random random = new Random();

        /// <summary>
        /// Returns a sanitized name based on the user's username.
        /// </summary>
        public static string SanitizedUserName()
        {
            int maxUserNameLength = 8;
            string userName = Environment.UserName;

            // if the user name is not available in the environment
            if (string.IsNullOrEmpty(userName))
            {
                return RandomString(maxUserNameLength).Sha256Hash(maxUserNameLength).ToLowerInvariant();
            }

            Regex r = new Regex("[^a-zA-Z0-9]");
            userName = r.Replace(userName, string.Empty).ToLowerInvariant();
            if (string.IsNullOrEmpty(userName))
            {
                return Environment.UserName.Sha256Hash(maxUserNameLength).ToLowerInvariant();
            }

            return userName;
        }

        /// <summary>
        /// Returns a random string of the specified length based on some alphanumeric values.
        /// </summary>
        /// <param name="length"></param>
        public static string RandomString(int length)
        {
            const string chars = "abcdef0123456789";
            var charsArray = chars.ToCharArray();
            var resultBuilder = new StringBuilder();
            for(int i=0;i<length;i++)
            {
                var shuffledCharacters = charsArray.Shuffle();
                resultBuilder.Append(shuffledCharacters.ElementAt(random.Next(charsArray.Length)));
            }
            return resultBuilder.ToString();
        }
    }
}