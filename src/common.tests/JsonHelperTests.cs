// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Json
{
    public class JsonHelperTests
    {
        [Fact]
        public void DeserializeObject_DeserializeStringWithReferenceLoop_ReturnsObject()
        {
            var serialized = string.Format("{{\"name\":\"Pippin\",\"bestFriend\":{{"
                 + "\"name\":\"Samwise\",\"bestFriend\":{{\"name\":\"Frodo\","
                 + "\"bestFriend\":null}}}}}}", Environment.NewLine);


            var deserialized = JsonHelpers.DeserializeObject<Person>(serialized);

            Assert.Equal("Pippin", deserialized.Name);
            Assert.Equal("Samwise", deserialized.BestFriend.Name);
            Assert.Equal("Frodo", deserialized.BestFriend.BestFriend.Name);
        }

        [Fact]
        public void DeserializeObject_DeserializeStringWithInvalidCase_ReturnsNull()
        {
            var serialized = "{\"Name\":\"Pippin\"}";
            var deserialized = JsonHelpers.DeserializeObject<Person>(serialized);
            Assert.Null(deserialized.Name);
        }

        [Fact]
        public void DeserializeObjectCaseInsensitive_DeserializeStringWithInvalidCase_ReturnsObject()
        {
            var serialized = "{\"Name\":\"Pippin\"}";
            var deserialized = JsonHelpers.DeserializeObjectCaseInsensitive<Person>(serialized);
            Assert.Equal("Pippin", deserialized.Name);
        }

        [Fact]
        public void SerializeForLoggingPurpose_SerializeString_ReturnsString()
        {
            const string expected = "Test string";
            Assert.Equal(expected, JsonHelpers.SerializeForLoggingPurpose(expected));
        }

        [Fact]
        public void SerializeForLoggingPurpose_SerializePII_ReturnsPIIValue()
        {
            var e = new PII("Frodo");
            var expected = e.Value;
            Assert.Equal(expected, JsonHelpers.SerializeForLoggingPurpose(e));
        }

        [Fact]
        public void SerializeForLoggingPurpose_SerilizeObject_ReturnsJson()
        {
            Person e = new Person()
            {
                Name = "Frodo"
            };
            var expected = "{\"name\":\"Frodo\",\"bestFriend\":null}";
            Assert.Equal(expected, JsonHelpers.SerializeForLoggingPurpose(e));
        }

        [Fact]
        public void SerializeForLoggingPurposeIndented_SerilaizeObject_ReturnsIndentedJson()
        {
            Person e = new Person()
            {
                Name = "Frodo"
            };
            var expected = string.Format("{{{0}  \"name\": \"Frodo\",{0}  \"bestFriend\": null{0}}}", Environment.NewLine);
            Assert.Equal(expected, JsonHelpers.SerializeForLoggingPurposeIndented(e));
        }

        [Fact]
        public void SerializeForLoggingPurpose_SerializeAggregateException_DoesNotContainModule()
        {
            Exception ex;
            try
            {
                try
                {
                    throw new Exception("TestException");
                }
                catch (Exception e)
                {
                    throw new AggregateException(e);
                }
            }
            catch (Exception e)
            {
                ex = e;
            }

            Assert.DoesNotContain("module", JsonHelpers.SerializeForLoggingPurpose(ex));
        }

        [Fact]
        public void SerializeObject_SerilaizeObject_ReturnsJson()
        {
            Person e = new Person()
            {
                Name = "Frodo"
            };
            var expected = "{\"name\":\"Frodo\",\"bestFriend\":null}";
            Assert.Equal(expected, JsonHelpers.SerializeObject(e));
        }

        [Fact]
        public void SerializeObjectIndented_SerilaizeObject_ReturnsIndentedJson()
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
        public void SerializeObject_SerializeObjectWithReferenceLoop_ReturnsJson()
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
            var expected = "{\"name\":\"Samwise\",\"bestFriend\":{\"name\":\"Frodo\",\"bestFriend\":null}}";
            Assert.Equal(expected, JsonHelpers.SerializeObject(p2));
        }

        [Fact]
        public void SerializeObject_SerializeWatchEvent_ReturnsJson()
        {
            var json = "{  \"type\": \"ADDED\",  \"object\": {\"kind\": \"Pod\", \"apiVersion\": \"v1\", \"metadata\": {\"resourceVersion\": \"10596\"}}}";
            var deserialized = JsonHelpers.DeserializeObject<WatchEvent>(json);

            Assert.Equal(WatchEventType.Added, deserialized.Type);
        }


        private class Person
        {
            public string Name { get; set; }
            public Person BestFriend { get; set; }
        }

        private class WatchEvent
        {
            public WatchEventType Type { get; set; }
            public KubernetesObject Object { get; set; }
        }
    }
}