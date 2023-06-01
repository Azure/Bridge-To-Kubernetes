// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
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

        /// <summary>
        /// Gets the value from a property in a Json string
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        public static T ParseAndGetProperty<T>(string json, string property)
        {
            var obj = JsonNode.Parse(json);
            return obj[property].GetValue<T>();
        }

    }
}