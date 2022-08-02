// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class EscapeUtilitiesTests
    {
        [Theory]
        [InlineData("hello world", "hello world")]
        [InlineData("hello $world", "hello $world")]
        [InlineData("\"hello world\"", "\\\"hello world\\\"")]
        [InlineData("\\hello world\\", "\\\\hello world\\\\")]
        [InlineData("\\\"hello world\\\"", "\\\\\\\"hello world\\\\\\\"")]
        [InlineData("`hello world`", "\\`hello world\\`")]
        [InlineData(null, null)]
        [InlineData("", "")]
        public void EscapeQuoteTest(string testString, string expectedEscapedString)
        {
            Assert.Equal(expectedEscapedString, EscapeUtilities.EscapeDoubleQuoteString(testString));
        }

        [Theory]
        [InlineData("hello", "hello")]
        [InlineData("hello world", "\"hello world\"")]
        [InlineData("\"hello world\"", "\"\\\"hello world\\\"\"")]
        [InlineData("\\\"hello world\\\"", "\"\\\\\\\"hello world\\\\\\\"\"")]
        public void EncodeParameterTest(string testString, string expectedEncodedString)
        {
            Assert.Equal(expectedEncodedString, EscapeUtilities.EncodeParameterArgument(testString));
        }

        [Theory]
        [InlineData("MYTOKEN=${secret.mytoken.token}", "MYTOKEN", "", "mytoken", "token", "")]
        [InlineData("TEST=${secret.testerapp-test.token}", "TEST", "", "testerapp-test", "token", "")]
        [InlineData(@"FOO=${secret.foo\.bar.token}", "FOO", "", "foo.bar", "token", "")]
        [InlineData(@"BAR=${secret.foo.foo\.bar\.token}", "BAR", "", "foo", "foo.bar.token", "")]
        [InlineData(@"HAS_PREFIX=hello-${secret.foo.bar}", "HAS_PREFIX", "hello-", "foo", "bar", "")]
        [InlineData(@"HAS_POSTFIX=${secret.foo.token}-hello", "HAS_POSTFIX", "", "foo", "token", "-hello")]
        [InlineData(@"HAS_BOTH=1234${secret.foo.token}4321", "HAS_BOTH", "1234", "foo", "token", "4321")]
        public void ParseBuildArgSecretTest(string inputArg, string expectedBuildArgName, string expectedPrefix, string expectedSecretName, string expectedSecretKey, string expectedPostfix)
        {
            (var buildArgName, var prefix, var secretName, var secretKey, var postfix) = EscapeUtilities.ParseBuildArgSecret(inputArg);
            Assert.Equal(expectedBuildArgName, buildArgName);
            Assert.Equal(expectedPrefix, prefix);
            Assert.Equal(expectedSecretName, secretName);
            Assert.Equal(expectedSecretKey, secretKey);
            Assert.Equal(expectedPostfix, postfix);
        }
    }
}