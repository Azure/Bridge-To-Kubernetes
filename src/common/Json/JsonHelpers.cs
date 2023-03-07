// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Xml;

namespace Microsoft.BridgeToKubernetes.Common.Json
{
    internal static class JsonHelpers
    {
        public static Encoding DefaultEncoding { get; } = Encoding.UTF8;

        private static IList<JsonConverter> DefaultConverters { get; } = new List<JsonConverter>
        {
            new Iso8601TimeSpanConverter(),
            new KubernetesDateTimeConverter(),
            new KubernetesDateTimeOffsetConverter(),
            new IntOrStringJsonConverter(),
            new IPAddressConverter(),
            new DictionaryStringObjectJsonConverter(),
            new PIIJsonConverter(),
            new SystemTextJsonPatch.Converters.JsonPatchDocumentConverterFactory(),
            new JsonStringEnumConverterEx<k8s.WatchEventType>()
        };

        private static JsonSerializerOptions SerializerOptions { get; } = CreateSerializerOptions();

        private static JsonSerializerOptions SerializerSettingsIndented { get; } = CreateSerializerOptions(indented: true);

        private static JsonSerializerOptions SerializerSettingsCaseInsensitive { get; } = CreateSerializerOptions(caseInsensitive: true);

        private static JsonSerializerOptions SerializerSettingsDefaultContractResolver { get; } = CreateSerializerOptions(camelCaseContextResolver: false);

        public static T DeserializeObject<T>(string v)
            => JsonSerializer.Deserialize<T>(v, SerializerOptions);

        public static T DeserializeObjectCaseInsensitive<T>(string v)
            => JsonSerializer.Deserialize<T>(v, SerializerSettingsCaseInsensitive);

        public static object DeserializeObject(string v)
            => JsonSerializer.Deserialize<object>(v, SerializerOptions);

        public static string SerializeObject(object obj, JsonSerializerOptions settings = null)
            => JsonSerializer.Serialize(obj, settings != null ? settings : SerializerOptions);

        public static string SerializeObjectIndented(object obj)
            => JsonSerializer.Serialize(obj, SerializerSettingsIndented);

        public static string SerializeObjectDefaultContractResolver(object obj)
            => JsonSerializer.Serialize(obj, SerializerSettingsDefaultContractResolver);

        public static Dictionary<string, string> DeserializeDictionary(string json)
            => DeserializeObject<Dictionary<string, string>>(json);

        public static string SerializeDictionary(IDictionary<string, string> d)
            => SerializeObject(d);

        public static string SerializeDictionaryIndented(IDictionary<string, string> d)
            => SerializeObjectIndented(d);

        public static dynamic ParseJson(string s)
            => (dynamic)JsonNode.Parse(s);

        public static T ParseAndGetProperty<T>(string json, string property)
        {
            var obj = JsonNode.Parse(json);
            return obj[property].GetValue<T>();
        }

        public static JsonSerializerOptions CreateSerializerOptions(bool indented = false, bool camelCaseContextResolver = true, bool caseInsensitive = false)
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = camelCaseContextResolver ? JsonNamingPolicy.CamelCase : null,
                WriteIndented = indented,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                PropertyNameCaseInsensitive = caseInsensitive,

                ReferenceHandler = ReferenceHandler.IgnoreCycles,
            };

            foreach (var converter in DefaultConverters)
            {
                jsonSerializerOptions.Converters.Add(converter);
            }

            return jsonSerializerOptions;
        }

        #region Private classes

        private class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override IPAddress? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var data = reader.GetString();
                if (data == null)
                    return null;

                var IP = IPAddress.Parse(data);
                return IP;
            }

            public override void Write(Utf8JsonWriter writer, IPAddress value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString());
            }
        }

        private class DictionaryStringObjectJsonConverter : JsonConverter<IDictionary<string, object>>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeToConvert == typeof(IDictionary<string, object>) || typeToConvert == typeof(Dictionary<string, object>);
            }

            public override Dictionary<string, object> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException($"JsonTokenType was of type {reader.TokenType}, only objects are supported");
                }

                var dictionary = new Dictionary<string, object>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return dictionary;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException("JsonTokenType was not PropertyName");
                    }

                    var propertyName = reader.GetString();

                    if (string.IsNullOrWhiteSpace(propertyName))
                    {
                        throw new JsonException("Failed to get property name");
                    }

                    reader.Read();

                    dictionary.Add(propertyName!, ExtractValue(ref reader, options));
                }

                return dictionary;
            }

            public override void Write(Utf8JsonWriter writer, IDictionary<string, object> value, JsonSerializerOptions options)
            {
                // We don't need any custom serialization logic for writing the json.
                // Ideally, this method should not be called at all. It's only called if you
                // supply JsonSerializerOptions that contains this JsonConverter in it's Converters list.
                // Don't do that, you will lose performance because of the cast needed below.
                // Cast to avoid infinite loop: https://github.com/dotnet/docs/issues/19268
                JsonSerializer.Serialize(writer, (IDictionary<string, object>)value);
            }

            private object ExtractValue(ref Utf8JsonReader reader, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        if (reader.TryGetDateTime(out var date))
                        {
                            return date;
                        }
                        return reader.GetString();
                    case JsonTokenType.False:
                        return false;
                    case JsonTokenType.True:
                        return true;
                    case JsonTokenType.Null:
                        return null;
                    case JsonTokenType.Number:
                        if (reader.TryGetInt64(out var result))
                        {
                            return result;
                        }
                        return reader.GetDecimal();
                    case JsonTokenType.StartObject:
                        return Read(ref reader, null!, options);
                    case JsonTokenType.StartArray:
                        var list = new List<object>();
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            list.Add(ExtractValue(ref reader, options));
                        }
                        return list;
                    default:
                        throw new JsonException($"'{reader.TokenType}' is not supported");
                }
            }
        }

        private class PIIJsonConverter : JsonConverter<PII>
        {
            public override bool CanConvert(Type objectType)
            {
                return (objectType == typeof(PII));
            }

            public override PII Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return new PII(JsonElement.ParseValue(ref reader).ToString());
            }

            public override void Write(Utf8JsonWriter writer, PII value, JsonSerializerOptions options)
            {
                JsonSerializer.Serialize(writer, value);
            }
        }

        private class Iso8601TimeSpanConverter : JsonConverter<TimeSpan>
        {
            public override TimeSpan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var str = reader.GetString();
                return XmlConvert.ToTimeSpan(str);
            }

            public override void Write(Utf8JsonWriter writer, TimeSpan value, JsonSerializerOptions options)
            {
                var iso8601TimeSpanString = XmlConvert.ToString(value); // XmlConvert for TimeSpan uses ISO8601, so delegate serialization to it
                writer.WriteStringValue(iso8601TimeSpanString);
            }
        }

        private sealed class KubernetesDateTimeConverter : JsonConverter<DateTime>
        {
            private static readonly JsonConverter<DateTimeOffset> UtcConverter = new KubernetesDateTimeOffsetConverter();
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                return UtcConverter.Read(ref reader, typeToConvert, options).UtcDateTime;
            }

            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                UtcConverter.Write(writer, value, options);
            }
        }

        private class KubernetesDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
        {
            private const string SerializeFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.ffffffK";
            private const string Iso8601Format = "yyyy'-'MM'-'dd'T'HH':'mm':'ssK";

            public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var str = reader.GetString();
                return DateTimeOffset.ParseExact(str, new[] { Iso8601Format, SerializeFormat }, CultureInfo.InvariantCulture, DateTimeStyles.None);
            }

            public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(SerializeFormat));
            }
        }

        private class IntOrStringJsonConverter : JsonConverter<k8s.Models.IntstrIntOrString>
        {
            public override k8s.Models.IntstrIntOrString? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                switch (reader.TokenType)
                {
                    case JsonTokenType.String:
                        return reader.GetString();
                    case JsonTokenType.Number:
                        return reader.GetInt64();
                    default:
                        break;
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException();
                }

                k8s.Models.IntstrIntOrString? intOrString = null;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndObject)
                    {
                        return intOrString;
                    }

                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException();
                    }

                    // Get the property name
                    var propertyName = reader.GetString();

                    // Get the value.
                    reader.Read();

                    switch (reader.TokenType)
                    {
                        case JsonTokenType.String:
                            intOrString = new k8s.Models.IntstrIntOrString(reader.GetString());
                            break;
                        case JsonTokenType.Number:
                            intOrString = new k8s.Models.IntstrIntOrString(reader.GetInt64().ToString());
                            break;
                        default:
                            throw new NotSupportedException();
                    }
                }

                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer, k8s.Models.IntstrIntOrString value, JsonSerializerOptions options)
            {
                if (writer == null)
                {
                    throw new ArgumentNullException(nameof(writer));
                }

                var s = value?.Value;

                if (long.TryParse(s, out var intv))
                {
                    writer.WriteNumberValue(intv);
                    return;
                }

                writer.WriteStringValue(s);
            }
        }

        public class JsonStringEnumConverterEx<TEnum> : JsonConverter<TEnum> where TEnum : struct, System.Enum
        {
            private readonly Dictionary<TEnum, string> _enumToString = new Dictionary<TEnum, string>();
            private readonly Dictionary<string, TEnum> _stringToEnum = new Dictionary<string, TEnum>();

            public JsonStringEnumConverterEx()
            {
                var type = typeof(TEnum);
                var values = System.Enum.GetValues<TEnum>();

                foreach (var value in values)
                {
                    var enumMember = type.GetMember(value.ToString())[0];
                    var attr = enumMember.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                      .Cast<EnumMemberAttribute>()
                      .FirstOrDefault();

                    _stringToEnum.Add(value.ToString(), value);

                    if (attr?.Value != null)
                    {
                        _enumToString.Add(value, attr.Value);
                        _stringToEnum.Add(attr.Value, value);
                    }
                    else
                    {
                        _enumToString.Add(value, value.ToString());
                    }
                }
            }

            public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var stringValue = reader.GetString();

                if (_stringToEnum.TryGetValue(stringValue, out var enumValue))
                {
                    return enumValue;
                }

                return default;
            }

            public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(_enumToString[value]);
            }
        }
        #endregion Private classes
    }
}