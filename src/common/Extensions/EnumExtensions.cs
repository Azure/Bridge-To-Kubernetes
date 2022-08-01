// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace System
{
    using System.Linq;
    using Microsoft.BridgeToKubernetes.Common.Attributes;

    /// <summary>
    /// Extensions to Enums.
    /// </summary>
    internal static class EnumExtensions
    {
        /// <summary>
        /// Gets the value set by <c>StringValueAttribute</c> in the Enum value.
        /// </summary>
        /// <param name="value">The enum value.</param>
        public static string GetStringValue(this Enum value)
        {
            var valueField = value.GetType().GetField(value.ToString());
            var valueAttribute = valueField.GetCustomAttributes(typeof(StringValueAttribute), inherit: false).FirstOrDefault() as StringValueAttribute;
            if (valueAttribute == null)
            {
                return value.ToString();
            }
            else
            {
                return valueAttribute.Value;
            }
        }

        /// <summary>
        /// Gets the integer value of an enum as string.
        /// </summary>
        /// <param name="value">The enum value.</param>
        public static string GetIntValue(this Enum value)
        {
            try
            {
                int intValue = Convert.ToInt32(value);
                return Convert.ToString(intValue);
            }
            catch
            {
                return value.ToString();
            }
        }
    }
}