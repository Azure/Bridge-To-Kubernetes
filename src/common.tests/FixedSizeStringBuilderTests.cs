// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class FixedSizeStringBuilderTests
    {
        [Fact]
        public void AppendShort()
        {
            var sb = new FixedSizeStringBuilder(10);

            sb.AppendLine("123");
            sb.Append("45");

            Assert.Equal(sb.ToString(), $"123{Environment.NewLine}45");
            Assert.False(sb.MaxLengthReached);
        }

        [Fact]
        public void AppendLong()
        {
            var sb = new FixedSizeStringBuilder(10);

            sb.AppendLine("12345");
            sb.Append("678910");

            Assert.Equal(sb.ToString(), "...678910");
            Assert.True(sb.MaxLengthReached);
        }

        [Fact]
        public void AppendLongValue()
        {
            var sb = new FixedSizeStringBuilder(10);

            sb.Append("12345678910");

            Assert.Equal(sb.ToString(), "...2345678910");
            Assert.True(sb.MaxLengthReached);
        }

        [Fact]
        public void AppendLine()
        {
            var sb = new FixedSizeStringBuilder(10);

            sb.AppendLine("123");
            sb.AppendLine();
            sb.AppendLine();
            sb.Append("456789");

            Assert.Equal(sb.ToString(), $"...{Environment.NewLine}{Environment.NewLine}456789");
            Assert.True(sb.MaxLengthReached);
        }
    }
}