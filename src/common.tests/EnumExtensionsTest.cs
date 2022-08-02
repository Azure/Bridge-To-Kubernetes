// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Attributes;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class EnumExtensionsTest
    {
        private enum SomeEnum
        {
            [StringValue("value one")]
            ValueOne,

            ValueTwo
        }

        [Fact]
        public void StringValueForEnumShouldBeRetrieved()
        {
            // Arrange
            var enumVal = SomeEnum.ValueOne;

            // Act
            var stringVal = enumVal.GetStringValue();

            // Assert
            Assert.Equal("value one", stringVal);
        }

        [Fact]
        public void StringValueForEnumWithoutStringValuePropertyShouldBeTheEnumName()
        {
            // Arrange
            var enumVal = SomeEnum.ValueTwo;

            // Act
            var stringVal = enumVal.GetStringValue();

            // Assert
            Assert.Equal("ValueTwo", stringVal);
        }
    }
}