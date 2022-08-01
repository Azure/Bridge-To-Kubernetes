// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class ApiVersionHelperTests
    {
        [Fact]
        public void CompareEqualVersionNumbersShouldReturnZero()
        {
            // Arrange
            var version1 = "2.3";
            var version2 = "2.3";

            // Act
            var result = ApiVersionHelper.CompareVersions(version1, version2);

            // Assert
            Assert.Equal(0, result);
        }

        [Fact]
        public void CompareFirstMajorBeingNewerShouldReturnMoreThanZero()
        {
            // Arrange
            var version1 = "3";
            var version2 = "2.3";

            // Act
            var result = ApiVersionHelper.CompareVersions(version1, version2);

            // Assert
            Assert.True(result > 0, "First version being newer should have a positive value in the comparison");
        }

        [Fact]
        public void CompareFirstMinorBeingNewerShouldReturnMoreThanZero()
        {
            // Arrange
            var version1 = "2.41";
            var version2 = "2.30";

            // Act
            var result = ApiVersionHelper.CompareVersions(version1, version2);

            // Assert
            Assert.True(result > 0, "First version being newer should have a positive value in the comparison");
        }

        [Fact]
        public void CompareSecondMajorBeingNewerShouldReturnLessThanZero()
        {
            // Arrange
            var version1 = "3";
            var version2 = "5";

            // Act
            var result = ApiVersionHelper.CompareVersions(version1, version2);

            // Assert
            Assert.True(result < 0, "Second version being newer should have a negative value in the comparison");
        }

        [Fact]
        public void CompareSecondMinorBeingNewerShouldReturnLessThanZero()
        {
            // Arrange
            var version1 = "2.1";
            var version2 = "2.30";

            // Act
            var result = ApiVersionHelper.CompareVersions(version1, version2);

            // Assert
            Assert.True(result < 0, "Second version being newer should have a negative value in the comparison");
        }
    }
}