// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class ReleaseUtilitiesTests
    {
        [Fact]
        public void ValidateReleaseNameTrue()
        {
            var actualSpaceName = "space";
            var actualServiceName = "service";
            var expectedReleaseName = "bridge-a629eb-space-service";
            var generatedReleaseName = ReleaseUtilities.GenerateReleaseName(actualSpaceName, actualServiceName);
            Assert.Equal(expectedReleaseName, generatedReleaseName);
        }
    }
}