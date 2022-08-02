// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Json.Tests
{
    public class JsonPropertyHelpersTests
    {
        [Fact]
        public void GetJsonPropertyNameTest()
        {
            Assert.Equal("bar", typeof(JsonAttributeTestModel).GetJsonPropertyName(nameof(JsonAttributeTestModel.Foo)));
        }

        [Fact]
        public void GetJsonPropertyNameNoAttributeTest()
        {
            var x = typeof(JsonAttributeTestModel).GetJsonPropertyName(nameof(JsonAttributeTestModel.Baz));
            Assert.Null(x);
        }

        private class JsonAttributeTestModel
        {
            [JsonProperty("bar")]
            public string Foo { get; set; }

            public int Baz { get; set; }
        }
    }
}