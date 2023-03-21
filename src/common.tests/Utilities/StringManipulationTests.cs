// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Utilities
{
    public class StringManipulationTests
    {
        [Theory]
        [InlineData("BEGIN PRIVATE KEY\nuirethweuifhewiufhweiuofhweo\njweiorjweoifnewouhfousdgherufhsdiuhEND PRIVATE KEY\n", "KEY_WAS_REMOVED\n")]
        [InlineData("iurhuweifhoBEGIN PRIVATE KEY\nuirethweuifhewiufhweiuofhweo\njweiorjweoifnewouhfousdgherufhsdiuhEND PRIVATE KEY\n", "iurhuweifhoKEY_WAS_REMOVED\n")]
        [InlineData("BEGIN PRIVATE KEYb2kkkkkkkkkkkkkkEND PRIVATE KEY", "KEY_WAS_REMOVED")]
        [InlineData("hello world", "hello world")]
        [InlineData("BEGIN PRIVATE KEY\nuirethweuifhewiufhweiuofhweo\njweiorjweoifnewouhfousdgherufhsdiuhEND PRIVATE KEY", "KEY_WAS_REMOVED")]
        public void RemovePrivateKeyIfNeededTest(string input, string expected)
        {
            Assert.True(string.Equals(StringManipulation.RemovePrivateKeyIfNeeded(input), expected));
        }
    }
}