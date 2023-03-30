// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Library.Utilities;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class PatternMatchingUtillitiesTests : TestsBase
    {
        [Theory]
        [InlineData("default-token", "default-token", true)]
        [InlineData("default-token-*", "default-token-temp", true)]
        [InlineData("default-Token-*", "default-token-temp", true)]
        [InlineData("default-Token-*-andy-*", "default-token-lars-andy-bert", true)]
        [InlineData("default-\\n{0}-token-*", "default-\\n{0}-token-temp-temp", true)]
        [InlineData("wrong-token", "default-token", false)]
        [InlineData("wrong-token-*", "default-token-test", false)]
        public void IsMatch(string inputPattern, string input, bool matchFound)
        {
            var isMatch = PatternMatchingUtillities.IsMatch(inputPattern, input);
            Assert.Equal(matchFound, isMatch);
        }
    }
}
