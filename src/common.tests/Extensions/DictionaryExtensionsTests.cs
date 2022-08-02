// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Extensions
{
    public class DictionaryExtensionsTests
    {
        [Fact]
        public void ValidateWithNull()
        {
            // Arrange
            IDictionary<string, string> dict = null;

            // Assert
            Assert.Throws<NullReferenceException>(() =>
            {
                dict.GetOrAdd("nullValue", () => "Should throw exception");
            });
        }

        [Fact]
        public void ValidateReturnsNullValue()
        {
            // Arrange
            IDictionary<string, string> dict = new Dictionary<string, string>();

            // Act
            var actual = dict.GetOrAdd("nullValue", () => null);

            // Assert
            Assert.Null(actual);
        }

        [Fact]
        public void SetNullValidateReturnValue()
        {
            // Arrange
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var key = "nullValue";

            // Act
            dict.GetOrAdd(key, () => null);

            // Assert
            Assert.Null(dict[key]);
        }

        [Fact]
        public void ValidateValueFactoryIsCalledOnce()
        {
            // Arrange
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var key = "currentDate";
            Func<string> valueFactory = () => DateTime.UtcNow.ToString("yyyyMMddHHmmssFFF");

            // Act
            var expected = dict.GetOrAdd(key, valueFactory);
            var actual = dict.GetOrAdd(key, valueFactory);

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateFactoryThrowsExceptions()
        {
            // Arrange
            IDictionary<string, string> dict = new Dictionary<string, string>();
            var key = "currentDate";
            Func<string> valueFactory = () => throw new ArithmeticException("DummyException");

            // Assert
            Assert.Throws<ArithmeticException>(() =>
            {
                var expected = dict.GetOrAdd(key, valueFactory);
            });
        }
    }
}