// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// Utilities to escape command line arguments passed to shell.
    /// </summary>
    internal class EscapeUtilities
    {
        /// <summary>
        /// https://www.gnu.org/software/bash/manual/html_node/Double-Quotes.html
        /// Escape the input string. Example, "hello" => \"hello\"
        /// </summary>
        public static string EscapeDoubleQuoteString(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                s = s.Replace("\\", "\\\\");    // \ => \\
                s = s.Replace("\"", "\\\"");    // " => \"
                s = s.Replace("`", "\\`");      // ` => \`
            }
            return s;
        }

        /// <summary>
        /// Escape quotes and spaces when necessary.
        /// Referenced from https://stackoverflow.com/questions/5510343/escape-command-line-arguments-in-c-sharp
        ///     hello  ==>   hello
        ///     hello world  ==>   "hello world"
        ///     "hello world"  ==>   "\"hello world\""
        /// </summary>
        public static string EncodeParameterArgument(string original)
        {
            if (string.IsNullOrEmpty(original))
                return original;
            string value = Regex.Replace(original, @"(\\*)" + "\"", @"$1\$0");
            value = Regex.Replace(value, @"^(.*\s.*?)(\\*)$", "\"$1$2$2\"", RegexOptions.Singleline);

            return value;
        }

        /// <summary>
        /// A utility that parses a build arg of the form: BUILD_ARG={possible prefix}${secret.secretname.secretkey}{possible postfix}, where secret key or name contain escaped "." characters
        /// </summary>
        /// <param name="line"></param>
        /// <returns>A tuple containing: (name of the build arg, prefix to the secret, name of the secret, the secret key, postfix to the secret)</returns>
        public static (string, string, string, string, string) ParseBuildArgSecret(string line)
        {
            var halves = line.Split('=');
            var buildArgName = halves[0];
            var secret = halves[1];

            var prefix = secret.Substring(0, secret.IndexOf("$"));
            var postfix = secret.Substring(secret.IndexOf("}") + 1);

            var slices = new List<string>();
            for (int prev = 0, curr = 0; curr < secret.Length; curr++)
            {
                if (secret.ElementAt(curr) == '\\')
                {
                    curr++;
                }
                else if (secret.ElementAt(curr) == '.' || secret.ElementAt(curr) == '}')
                {
                    slices.Add(secret.Substring(prev, curr - prev).Replace("\\", string.Empty));
                    prev = curr + 1;
                }
            }
            return (buildArgName, prefix, slices[1], slices[2], postfix);
        }
    }
}