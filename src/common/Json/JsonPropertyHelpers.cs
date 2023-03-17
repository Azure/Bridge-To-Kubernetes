// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;

namespace System
{
    /// <summary>
    /// Helper functions for JsonProperties
    /// </summary>
    internal static class JsonPropertyHelpers
    {
        /// <summary>
        /// Given a type, and property name from that type, return the JsonPropertyNameAttribute.Name
        /// </summary>
        /// <param name="t"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public static string GetJsonPropertyName(this Type t, string propertyName)
        {
            return t.GetProperties()
                .First(p => p.Name == propertyName)
                .GetCustomAttribute<JsonPropertyNameAttribute>(false)?
                .Name;
        }

        /// <summary>
        /// Gets the JsonPropertyNameAttribute.Name from the PropertyInfo, if exists
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static string GetJsonPropertyName(this PropertyInfo p)
        {
            return p.GetCustomAttribute<JsonPropertyNameAttribute>(false)?.Name;
        }
    }
}