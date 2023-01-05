// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Json.Tests
{
    public class JsonHelpersTest
    {
        private readonly BridgeJsonSerializerSettings serializerSettings = new BridgeJsonSerializerSettings()
        {
            MaxDepth = 1,
            ReferenceLoopHandling = BridgeReferenceLoopHandling.Error
        };

        [Fact]
        public void CreateSerializerSettingsDefault()
        {
            var settings = (JsonSerializerSettings)JsonHelpers.CreateSerializerSettings();
            Assert.Equal(Formatting.None, settings.Formatting);
            Assert.IsType<STJCamelCaseContractResolver>(settings.ContractResolver);
        }

        [Fact]
        public void CreateSerializerSettingsCustomIndentation()
        {
            var settings = (JsonSerializerSettings)JsonHelpers.CreateSerializerSettings(indented: true);
            Assert.Equal(Formatting.Indented, settings.Formatting);
            Assert.IsType<STJCamelCaseContractResolver>(settings.ContractResolver);
        }

        [Fact]
        public void CreateSerializerSettingsCustomCasing()
        {
            var settings = (JsonSerializerSettings)JsonHelpers.CreateSerializerSettings(camelCaseContextResolver: false);
            Assert.Equal(Formatting.None, settings.Formatting);
            Assert.IsType<STJContractResolver>(settings.ContractResolver);
        }

        [Fact]
        public void ConvertSerializerSettingsSuccessful()
        {
            var settings = (JsonSerializerSettings)JsonHelpers.ConvertSerializerSettings(serializerSettings);
            Assert.Equal(serializerSettings.MaxDepth, settings.MaxDepth);
            Assert.Equal(BridgeReferenceLoopHandling.Error.ToString(), settings.ReferenceLoopHandling.ToString());
        }

        [Fact]
        public void SerializeObjectSuccessful()
        {
            Person e = new Person()
            {
                Name = "Frodo"
            };
            var expected = "{\"name\":\"Frodo\",\"bestFriend\":null}";
            Assert.Equal(expected, JsonHelpers.SerializeObject(e));
        }

        [Fact]
        public void SerializeObjectIndentedSuccessful()
        {
            Person e = new Person()
            {
                Name = "Frodo"
            };
            var expected = string.Format("{{{0}  \"name\": \"Frodo\",{0}  \"bestFriend\": null{0}}}", Environment.NewLine);
            var result = JsonHelpers.SerializeObjectIndented(e);
            Assert.Equal(expected, result);
            Assert.True(result.Split("\n").Length > 1);
        }

        [Fact]
        public void SerializeObjectWithReferenceLoopDefaultSettings()
        {
            Person p1 = new Person()
            {
                Name = "Frodo"
            };

            Person p2 = new Person()
            {
                Name = "Samwise",
                BestFriend = p1
            };

            p1.BestFriend = p2;
            var expected = "{\"name\":\"Samwise\",\"bestFriend\":{\"name\":\"Frodo\"}}";
            Assert.Equal(expected, JsonHelpers.SerializeObject(p2));
        }

        [Fact]
        public void SerializeObjectWithReferenceLoopCustomSettings()
        {
            Person p1 = new Person()
            {
                Name = "Frodo"
            };

            Person p2 = new Person()
            {
                Name = "Samwise",
                BestFriend = p1
            };

            p1.BestFriend = p2;
            Assert.Throws<JsonSerializationException>(() => JsonHelpers.SerializeObject(p2, serializerSettings));
        }

        [Fact]
        public void SerializeAndDeserializeDefaultSettings()
        {
            Person p1 = new Person()
            {
                Name = "Frodo"
            };

            Person p2 = new Person()
            {
                Name = "Samwise",
                BestFriend = p1
            };

            Person p3 = new Person()
            {
                Name = "Pippin",
                BestFriend = p2
            };

            var serialized = JsonHelpers.SerializeObject(p3);
            var deserialized = JsonHelpers.DeserializeObject(serialized);
            var expected = string.Format("{{{0}  \"name\": \"Pippin\",{0}  \"bestFriend\": {{{0}"
                + "    \"name\": \"Samwise\",{0}    \"bestFriend\": {{{0}      \"name\": \"Frodo\",{0}"
                + "      \"bestFriend\": null{0}    }}{0}  }}{0}}}", Environment.NewLine);
            Assert.Equal(expected, deserialized.ToString());
        }

        private class Person
        {
            public string Name { get; set; }
            public Person BestFriend { get; set; }
        }
    }
}