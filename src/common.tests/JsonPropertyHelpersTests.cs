// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text.Json.Serialization;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Json
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
            [JsonPropertyName("bar")]
            public string Foo { get; set; }

            public int Baz { get; set; }
        }
    }
}