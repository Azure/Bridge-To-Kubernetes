// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace System
{
    internal static class StringExtensions
    {
        private const int maxHashLength = 64;

        /// <summary>
        /// Finds if a string is of a specific pattern type
        /// </summary>
        /// <param name="str">The string on which the pattern is matched</param>
        /// <param name="pattern">The pattern required in the str</param>
        /// <returns>True if pattern is present, false otherwise</returns>
        public static bool Like(this string str, string pattern)
        {
            return new Regex(
                "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Singleline
            ).IsMatch(str);
        }

        /// <summary>
        /// Escapes braces in a string so they are not considered a pattern to be replaced in <c>String.Format</c>.
        /// </summary>
        /// <param name="s">The string to have the braces escaped.</param>
        /// <returns>String with the braces escaped.</returns>
        public static string EscapeBraces(this string s)
        {
            return s
                .Replace("{", "{{")
                .Replace("}", "}}");
        }

        /// <summary>
        /// Calculate a stable hash using SHA256
        /// </summary>
        /// <param name="str">The string to Hash</param>
        /// <param name="length">The length of the hash (max 64)</param>
        /// <remarks>Use only for hashing, do not use as cryptographic function</remarks>
        /// <returns></returns>
        public static string Sha256Hash(this string str, int length = maxHashLength)
        {
            if (length > maxHashLength)
            {
                throw new ArgumentException($"Unable to calculate a hash longer than {maxHashLength} characters");
            }
            using (var algorithm = System.Security.Cryptography.SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(str));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString().Substring(0, length).ToLowerInvariant();
            }
        }

        /// <summary>
        /// Converts the string to base 64 from its UTF8 representation.
        /// </summary>
        /// <param name="str">The string to be converted.</param>
        /// <returns>Base 64 representation of the string.</returns>
        public static string ToBase64(this string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            var base64 = Convert.ToBase64String(bytes);
            return base64;
        }

        /// <summary>
        /// Converts a base64 encoded string to its UTF8 representation.
        /// </summary>
        /// <param name="str">The string to be converted.</param>
        /// <returns>Decoded UTF8 representation.</returns>
        public static string FromBase64(this string str)
        {
            var bytes = Convert.FromBase64String(str);
            var decodedString = Encoding.UTF8.GetString(bytes);
            return decodedString;
        }

        /// <summary>
        /// Convert a string to UTF8 bytes
        /// </summary>
        public static byte[] ToUtf8(this string str)
        {
            return Encoding.UTF8.GetBytes(str);
        }

        /// <summary>
        /// Convert UTF8 bytes to a string
        /// </summary>
        public static string Utf8ToString(this byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        public static Stream ToStream(this string str)
        {
            return new MemoryStream(str.ToUtf8());
        }

        /// <summary>
        /// Perform token replacement for the given string. Please note the result is undefined
        /// if the replaced string may contain replacement tokens.
        /// </summary>
        public static string Replace(this string str, IDictionary<string, string> replacements)
        {
            var builder = new StringBuilder(str);

            foreach (var replacement in replacements)
            {
                builder.Replace(replacement.Key, replacement.Value);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Uses OrdinalIgnoreCase
        /// </summary>
        public static bool EqualsIgnoreCase(this string str, string other)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(str, other);
        }

        /// <summary>
        /// Uses OrdinalIgnoreCase
        /// </summary>
        public static bool ContainsIgnoreCase(this string str, string other)
        {
            return str.IndexOf(other, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}