// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class PIIJsonConverter : JsonConverter<PII>
    {
        public override bool CanConvert(Type objectType)
        {
            return (objectType == typeof(PII));
        }

        public override PII Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new PII(JsonNode.Parse(ref reader).ToString());
        }

        public override void Write(Utf8JsonWriter writer, PII value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}