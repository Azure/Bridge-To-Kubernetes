// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    /// <summary>
    /// Tests to ensure embedded files work as expected
    /// </summary>
    public class EmbeddedFileTests : TestsBase
    {
        private readonly ImageProvider _imageProvider;

        public EmbeddedFileTests()
        {
            _imageProvider = _autoFake.Resolve<ImageProvider>();
        }

        [Fact]
        public void DevHostAgentImageTag()
            => ImageTagTest(() => _imageProvider.DevHostImage);

        [Fact]
        public void RestorationJobImageTag()
            => ImageTagTest(() => _imageProvider.DevHostRestorationJobImage);

        private static void ImageTagTest(Func<string> tagProperty)
        {
            string image = tagProperty.Invoke();
            Assert.False(string.IsNullOrWhiteSpace(image));
            int i = image.IndexOf(':');
            Assert.NotEqual(-1, i);
            string tag = image.Substring(i + 1);
            Assert.False(string.IsNullOrWhiteSpace(tag));
        }
    }
}