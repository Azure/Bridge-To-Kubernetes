// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class PathUtilitiesTests : TestsBase
    {
        private static readonly IPlatform Platform = new Platform();

        private static readonly IEnumerable<object[]> _windowsUris = new string[][]
        {
            new string[] { @"C:\Users\username", @"C:\Users\username\file.txt", "file.txt" },
            new string[] { @"C:\Users", @"C:\Users\username\file.txt", @"username\file.txt" },
            new string[] { @"C:\Users\foo", @"C:\file.txt", @"..\..\file.txt" }
        };

        private static readonly IEnumerable<object[]> _unixUris = new string[][]
        {
            new string[] { "/Users", "/Users/username/file.txt", "username/file.txt" },
            new string[] { "/Users/username", "/Users/username/file.txt", "file.txt" },
            new string[] { "/Users/foo", "/file.txt", "../../file.txt" }
        };

        private readonly PathUtilities _pathUtilities;

        public PathUtilitiesTests()
        {
            _autoFake.Provide(Platform);
            _pathUtilities = _autoFake.Resolve<PathUtilities>();
        }

        #region ValidateMakeRelativeUri

        // PathUtilities relies on System.IO.Path, which doesn't behave the same on Unix-based OS's as it does on Windows.
        // So, use test case data w.r.t. the current platform.
        // We run tests on all platforms so all cases should be hit.
        public static readonly IEnumerable<object[]> ValidateMakeRelativeUriTestData = Platform.IsWindows ? _windowsUris : _unixUris;

        [Theory]
        [MemberData(nameof(ValidateMakeRelativeUriTestData))]
        public void ValidateMakeRelativeUri(string basePath, string path, string expected)
        {
            Assert.Equal(expected, _pathUtilities.MakeRelative(basePath, path));
        }

        [Fact]
        public void ValidateMakeRelativeUri_RelativeBasePathThrows()
        {
            Assert.Throws<ArgumentException>(() => _pathUtilities.MakeRelative("foo", "bar"));
        }

        #endregion ValidateMakeRelativeUri
    }
}