// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Json
{
    internal static class JsonHelpers
    {
        public static Encoding DefaultEncoding { get; } = Encoding.UTF8;

        private static IContractResolver ContractResolver { get; } = new STJCamelCaseContractResolver();

        private static IList<JsonConverter> DefaultConverters { get; } = new List<JsonConverter>
        {
            new IsoDateTimeConverter
            {
                DateTimeFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ",
                DateTimeStyles = System.Globalization.DateTimeStyles.AdjustToUniversal
            },
            new IPAddressConverter()
        };

        private static JsonSerializerSettings SerializerSettings { get; } = (JsonSerializerSettings)CreateSerializerSettings();

        private static JsonSerializerSettings SerializerSettingsIndented { get; } = (JsonSerializerSettings)CreateSerializerSettings(indented: true);

        private static JsonSerializerSettings SerializerSettingsDefaultContractResolver { get; } = (JsonSerializerSettings)CreateSerializerSettings(camelCaseContextResolver: false);

        public static T DeserializeObject<T>(string v)
            => JsonConvert.DeserializeObject<T>(v, SerializerSettings);

        public static object DeserializeObject(string v)
            => JsonConvert.DeserializeObject(v, SerializerSettings);

        public static string SerializeObject(object obj, BridgeJsonSerializerSettings settings = null)
            => JsonConvert.SerializeObject(obj, settings != null ? (JsonSerializerSettings)ConvertSerializerSettings(settings) : SerializerSettings);

        public static string SerializeObjectIndented(object obj)
            => JsonConvert.SerializeObject(obj, SerializerSettingsIndented);

        public static string SerializeObjectDefaultContractResolver(object obj)
            => JsonConvert.SerializeObject(obj, SerializerSettingsDefaultContractResolver);

        public static Dictionary<string, string> DeserializeDictionary(string json)
            => DeserializeObject<Dictionary<string, string>>(json);

        public static string SerializeDictionary(IDictionary<string, string> d)
            => SerializeObject(d);

        public static string SerializeDictionaryIndented(IDictionary<string, string> d)
            => SerializeObjectIndented(d);

        public static dynamic ParseJson(string s)
            => (dynamic)JObject.Parse(s);

        public static T ParseAndGetProperty<T>(string json, string property)
        {
            var obj = JObject.Parse(json);
            return obj[property].Value<T>();
        }

        /// <returns>Returns 'object' to prevent binding issues when clients reference specific versions of Newtonsoft.Json</returns>
        public static object CreateSerializerSettings(bool indented = false, bool camelCaseContextResolver = true)
        {
            IContractResolver contractResolver = camelCaseContextResolver ? (IContractResolver)new STJCamelCaseContractResolver() : new STJContractResolver();
            return new JsonSerializerSettings
            {
                Formatting = indented ? Formatting.Indented : Formatting.None,
                TypeNameHandling = TypeNameHandling.None, // SDL Requirements dictate that we use this value
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                ContractResolver = contractResolver,
                Converters = DefaultConverters,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                Error = (object sender, ErrorEventArgs errorArgs) =>
                {
                    // Ignore deserialization errors (typically caused by older documents not conforming to current schemas)
                    var currentError = errorArgs.ErrorContext.Error.Message;
                    errorArgs.ErrorContext.Handled = true;

                    if (Debugger.IsAttached)
                    {
                        // Failed to deserialize. Have you changed the schema of a docDb document? Be sure to migrate existing documents and/or handle the missing properties in consumers.
                        Debugger.Break();
                    }
                }
            };
        }

        /// <returns>Returns 'object' to prevent binding issues when clients reference specific versions of Newtonsoft.Json</returns>
        internal static object ConvertSerializerSettings(BridgeJsonSerializerSettings settings)
        {
            var serializerSettings = new JsonSerializerSettings()
            {
                MaxDepth = settings.MaxDepth
            };
            if (settings.ReferenceLoopHandling != null)
            {
                serializerSettings.ReferenceLoopHandling = (ReferenceLoopHandling)Enum.Parse(typeof(ReferenceLoopHandling), settings.ReferenceLoopHandling.ToString(), true);
            }
            if (settings.Ignores != null)
            {
                serializerSettings.ContractResolver = new PropertyIgnoreSerializerContractResolver(settings.Ignores);
            }

            return serializerSettings;
        }

        #region Private classes

        /// <summary>
        /// This contract resolver is used to ignore particular fields when serializing.
        /// TODO: Should this derive from a camelcase resolver?  Not sure of use cases for this.
        /// </summary>
        private class PropertyIgnoreSerializerContractResolver : DefaultContractResolver
        {
            private readonly Dictionary<Type, HashSet<string>> _ignores;

            public PropertyIgnoreSerializerContractResolver(Dictionary<Type, HashSet<string>> ignores = null)
            {
                _ignores = ignores ?? new Dictionary<Type, HashSet<string>>();
            }

            /// <summary>
            /// Add a property to ignore
            /// </summary>
            /// <param name="type">The type containing the property to ignore. Any object of this type or any of its inherited types will be considered.</param>
            /// <param name="jsonPropertyNames">The names of the properties to ignore</param>
            public void IgnoreProperty(Type type, params string[] jsonPropertyNames)
            {
                if (!_ignores.ContainsKey(type))
                    _ignores[type] = new HashSet<string>();

                foreach (var prop in jsonPropertyNames)
                    _ignores[type].Add(prop);
            }

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);

                if (IsIgnored(property.DeclaringType, property.PropertyName))
                {
                    property.ShouldSerialize = i => false;
                    property.Ignored = true;
                }
                return property;
            }

            private bool IsIgnored(Type type, string jsonPropertyName)
            {
                return _ignores.Any() && _ignores.Where(i => (type == i.Key || type.IsSubclassOf(i.Key)) && i.Value.Contains(jsonPropertyName)).Any();
            }
        }

        private class IPAddressConverter : JsonConverter<IPAddress>
        {
            public override IPAddress ReadJson(JsonReader reader, Type objectType, IPAddress existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var data = serializer.Deserialize<string>(reader);
                if (data == null)
                    return null;
                var IP = IPAddress.Parse(data);
                return IP;
            }

            public override void WriteJson(JsonWriter writer, IPAddress value, JsonSerializer serializer)
            {
                writer.WriteValue(value.ToString());
            }
        }

        #endregion Private classes
    }
}