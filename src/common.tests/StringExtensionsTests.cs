// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class StringExtensionsTests
    {
        [Fact]
        public void JsonObjectShouldHaveBracesEscaped()
        {
            // Arrange
            var jsonObj = "{a : { c : 123 }}";

            // Act
            var escapedObj = jsonObj.EscapeBraces();

            // Assert
            Assert.Equal("{{a : {{ c : 123 }}}}", escapedObj);
        }

        [Fact]
        public void StringShouldBeConvertedToBase64()
        {
            // Arrange
            var srcString = "This is a string";

            // Act
            var targetString = srcString.ToBase64();

            // Assert
            Assert.Equal("VGhpcyBpcyBhIHN0cmluZw==", targetString);
        }
    }
}