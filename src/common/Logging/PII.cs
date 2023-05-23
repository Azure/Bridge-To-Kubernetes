// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Safely handle a logged string that may contain personally-identifiable information
    /// </summary>
    [JsonConverter(typeof(PIIJsonConverter))]
    internal class PII
    {
        private string _value;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="value">A value that may contain personally-identifiable information that should be anonymized</param>
        public PII(string value)
        {
            Value = value ?? string.Empty;
        }

        /// <summary>
        /// A scrambled version of the string to preseve anonymity
        /// </summary>
        [JsonIgnore]
        public string ScrambledValue { get; private set; }

        /// <summary>
        /// The unscrambled value that may contain personally-identifiable information
        /// </summary>
        [JsonPropertyName("Value")]
        public string Value
        {
            get { return _value; }
            set
            {
                _value = value;
                ScrambledValue = _value?.Sha256Hash(length: 16);
            }
        }

        /// <summary>
        /// The unscrambled value that may contain personally-identifiable information
        /// </summary>
        public override string ToString()
        {
            return Value;
        }

        /// <summary>
        /// Test for equality between the <see cref="Value"/> of this instance and that of <paramref name="obj"/>
        /// </summary>
        /// <param name="obj">The object to test</param>
        /// <returns>True if <paramref name="obj"/> is an instance of <see cref="PII"/> with the same <see cref="Value"/> as the one in this instance, otherwise false</returns>
        public override bool Equals(object obj)
        {
            var item = obj as PII;

            if (item == null)
            {
                return false;
            }

            return this.Value.Equals(item.Value);
        }

        /// <summary>
        /// <see cref="object.GetHashCode"/>
        /// </summary>
        /// <returns>A hash code based on <see cref="Value"/></returns>
        public override int GetHashCode()
        {
            return this.Value.GetHashCode();
        }

        /// <summary>
        /// Uses <paramref name="GetPiiValue"/> to anonymize any potentially persionally-identifiable information in <paramref name="input"/> as necessary
        /// </summary>
        /// <param name="input">A value or set of values that may contain persionally-identifiable information</param>
        /// <param name="GetPiiValue">A callback used to anonymize each input string as necessary</param>
        /// <returns>A copy of <paramref name="input"/>, anonymized as necessary</returns>
        public static string SanitizeOutput(object input, Func<PII, string> GetPiiValue)
        {
            if (input is IEnumerable<object> children)
            {
                var sanitizedChildren = children.Select(child => SanitizeOutput(child, GetPiiValue));
                return string.Join(" ", sanitizedChildren);
            }

            if (input is IDictionary<string, object> properties)
            {
                var sanitizedProperties = properties.ToDictionary(p => p.Key, p => SanitizeOutput(p.Value, GetPiiValue));
                return JsonHelpers.SerializeForLoggingPurpose(sanitizedProperties);
            }

            if (input is Enum enumType) // for logging purpose, get the string value of an enum
            {
                input = enumType.GetStringValue();
            }

            return input is PII pii ? GetPiiValue(pii) : JsonHelpers.SerializeForLoggingPurpose(input);
        }
    }
}