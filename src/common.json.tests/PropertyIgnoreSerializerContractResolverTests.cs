// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Json.Tests
{
    public class PropertyIgnoreSerializerContractResolverTests
    {
        [Fact]
        public void IgnorePropertySameType()
        {
            BridgeJsonSerializerSettings serializerSettings = new BridgeJsonSerializerSettings();
            serializerSettings.Ignores = new Dictionary<Type, HashSet<string>>();
            serializerSettings.Ignores.Add(typeof(CustomType1), new HashSet<string> { "Field2" });

            var ob = new CustomType1();
            ob.Field1 = "test1";
            ob.Field2 = 2;
            var ser = JsonHelpers.SerializeObject(ob, serializerSettings);
            var expected = @"{""Field1"":""test1""}";
            Assert.Equal(expected, ser);
        }

        [Fact]
        public void IgnorePropertySubclass()
        {
            BridgeJsonSerializerSettings serializerSettings = new BridgeJsonSerializerSettings();
            serializerSettings.Ignores = new Dictionary<Type, HashSet<string>>();
            serializerSettings.Ignores.Add(typeof(CustomType1), new HashSet<string> { "Field2" });

            var ob = new CustomType2();
            ob.Field1 = "test1";
            ob.Field2 = 2;
            ob.Field3 = new String("test3");
            var ser = JsonHelpers.SerializeObject(ob, serializerSettings);
            var expected = @"{""Field3"":""test3"",""Field1"":""test1""}";
            Assert.Equal(expected, ser);
        }

        [Fact]
        public void EmptyIgnoreProperty()
        {
            BridgeJsonSerializerSettings serializerSettings = new BridgeJsonSerializerSettings();
            serializerSettings.Ignores = new Dictionary<Type, HashSet<string>>();

            var ob = new CustomType2();
            ob.Field1 = "test1";
            ob.Field2 = 2;
            ob.Field3 = new String("test3");
            var ser = JsonHelpers.SerializeObject(ob, serializerSettings);
            var expected = @"{""Field3"":""test3"",""Field1"":""test1"",""Field2"":2}";
            Assert.Equal(expected, ser);
        }
    }

    internal class CustomType1
    {
        public string Field1 { get; set; }
        public int Field2 { get; set; }
    }

    internal class CustomType2 : CustomType1
    {
        public object Field3 { get; set; }
    }
}