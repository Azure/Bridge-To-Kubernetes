// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text;
using Newtonsoft.Json.Linq;

namespace Newtonsoft.Json
{
    /// <summary>
    /// Converts string types to/from base64 when serializing/deserializing JSON
    /// </summary>
    internal class StringBase64Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(string).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            var str = token.Value<string>();
            return string.IsNullOrWhiteSpace(str) ? str : Encoding.UTF8.GetString(Convert.FromBase64String(str));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            string str = value as string;
            if (!string.IsNullOrEmpty(str))
            {
                serializer.Serialize(writer, Convert.ToBase64String(Encoding.UTF8.GetBytes(str)));
            }
            else
            {
                serializer.Serialize(writer, null);
            }
        }
    }
}