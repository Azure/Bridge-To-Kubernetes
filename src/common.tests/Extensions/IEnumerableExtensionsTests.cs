// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Extensions
{
    public class IEnumerableExtensionsTests
    {
        [Fact]
        public void ExceptTests()
        {
            var foo = new char[] { 'a', 'b', 'c' };
            var bar = new string[] { "A", "b", "c" };

            Assert.Equal(new char[] { 'a' }, foo.Except(bar, z => z[0]));
            Assert.Equal(new char[0], foo.Except(bar, z => z.ToString(), StringComparer.OrdinalIgnoreCase));
            Assert.Equal(new string[] { "A" }, bar.Except(foo, z => z[0]));
            Assert.Equal(new string[0], bar.Except(foo, z => z.ToString(), StringComparer.OrdinalIgnoreCase));
            Assert.Equal(new string[] { "A" }, bar.Except(foo, f => f, b => b.ToString()));
            Assert.Equal(new string[0], bar.Except(foo, f => f, b => b.ToString(), StringComparer.OrdinalIgnoreCase));
        }
    }
}