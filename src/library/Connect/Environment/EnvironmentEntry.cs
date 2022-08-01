// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal class EnvironmentEntry
    {
        public EnvironmentEntry(string name, string value, LocalProcessConfigFile config)
        {
            // parse for Name=Value
            if (name.Length > 255)
            {
                throw new ArgumentException($"Environment variable names must be shorter than 255 characters");
            }
            this.Name = name;
            this.Tokens = this.ParseValue(value, config);
        }

        /// <summary>
        /// List of tokens that compose the value of the environment entry
        /// </summary>
        public IEnumerable<EnvironmentTokenBase> Tokens { get; private set; }

        /// <summary>
        /// Name of the environment entry
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the evaluated value
        /// </summary>
        /// <param name="env"></param>
        /// <returns></returns>
        public string Evaluate()
        {
            if (Tokens == null || !Tokens.Any())
            {
                return string.Empty;
            }
            //TODO: depending on the type concatenation is not the right way, e.g. volume mounts on windows.
            StringBuilder sb = new StringBuilder();
            foreach (var t in Tokens)
            {
                sb.Append(t.Evaluate());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parse a valueString into a set of tokens.
        /// Tokens can then be replaced with values or copied verbatim into a final value when evaluating a <see cref="EnvironmentEntry"/>
        /// </summary>
        /// <param name="valueString">The value string to be parsed</param>
        /// <returns></returns>
        private IEnumerable<EnvironmentTokenBase> ParseValue(string valueString, LocalProcessConfigFile config)
        {
            List<EnvironmentTokenBase> tokens = new List<EnvironmentTokenBase>();
            int i = 0;
            while (i < valueString.Length)
            {
                bool isReplacementToken = valueString[i] == '$';
                int next = isReplacementToken ? valueString.IndexOf(')', i) : valueString.IndexOf('$', i) - 1;
                if (next < 0)
                {
                    next = valueString.Length - 1;
                }

                string tokenString = valueString.Substring(i, next - i + 1);

                tokens.Add(EnvironmentTokenParser.Parse(tokenString, isReplacementToken, config));

                i = next + 1;
            }
            return tokens;
        }
    }
}