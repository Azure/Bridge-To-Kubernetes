// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class TypeExtensionsTests
    {
        [Fact]
        public void IsBridgeToKubernetesTypeForStringShouldReturnFalse()
        {
            // Arrange
            string a = "abc";

            // Act
            var isPrimitive = a.GetType().IsBridgeToKubernetesType();

            // Assert
            Assert.False(isPrimitive, "string is a primitive type");
        }

        [Fact]
        public void IsBridgeToKubernetesTypeForDictionaryShouldReturnFalse()
        {
            // Arrange
            var map = new Dictionary<string, int>();

            // Act
            var isPrimitive = map.GetType().IsBridgeToKubernetesType();

            // Assert
            Assert.False(isPrimitive, "Dictionary is a system type");
        }

        [Fact]
        public void IsBridgeToKubernetesTypeForShouldReturnTrueForClientClass()
        {
            // Arrange
            var serviceBaseType = typeof(Microsoft.BridgeToKubernetes.Common.ServiceBase);

            // Act
            var isPrimitive = serviceBaseType.IsBridgeToKubernetesType();

            // Assert
            Assert.True(isPrimitive, "ServiceBase is a Bridge to Kubernetes type");
        }

        [Fact]
        public void DefaultValueForInt()
        {
            // Arrange
            int defaultVal = default(int);

            // Act
            var isDefaultVal = defaultVal.GetType().IsDefaultValue(defaultVal);

            Assert.True(isDefaultVal);
        }

        [Fact]
        public void DefaultValueForString()
        {
            // Arrange
            string defaultVal = default(string);

            // Act
            var isDefaultVal = typeof(string).IsDefaultValue(defaultVal);

            Assert.True(isDefaultVal);
        }

        [Fact]
        public void DefaultValueForClass()
        {
            // Arrange
            TypeExtensionsTests defaultVal = default(TypeExtensionsTests);

            // Act
            var isDefaultVal = typeof(TypeExtensionsTests).IsDefaultValue(defaultVal);

            Assert.True(isDefaultVal);
        }

        [Fact]
        public void DefaultValueForNullable()
        {
            // Arrange
            System.Nullable<bool> defaultVal = default(System.Nullable<bool>);

            // Act
            var isDefaultVal = typeof(System.Nullable<bool>).IsDefaultValue(defaultVal);

            Assert.True(isDefaultVal);
        }
    }
}