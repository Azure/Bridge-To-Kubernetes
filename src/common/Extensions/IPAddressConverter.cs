// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using Newtonsoft.Json.Linq;

namespace Newtonsoft.Json
{
    /// <summary>
    /// Custom JsonConverter for the System.Net.IPAddress type
    /// </summary>
    internal class IPAddressConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(IPAddress).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            JToken token = JToken.Load(reader);
            IPAddress ip;
            if (!IPAddress.TryParse(token.Value<string>(), out ip))
            {
                return null;
            }

            return ip;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, (value as IPAddress)?.ToString());
        }
    }
}