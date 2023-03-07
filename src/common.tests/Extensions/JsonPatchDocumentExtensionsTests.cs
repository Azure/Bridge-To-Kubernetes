// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Models;
using SystemTextJsonPatch;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Extensions
{
    public class JsonPatchDocumentExtensionsTests
    {
        [Fact]
        public void TestGetImageReplacement_Deployment()
        {
            const string image = "foobar";
            var patch = new JsonPatchDocument<V1Deployment>();
            patch.Replace(d => d.Spec.Template.Spec.Containers[0].Image, image);
            Assert.Equal(image, patch.TryGetContainerImageReplacementValue());

            patch.Operations.Clear();
            Assert.Null(patch.TryGetContainerImageReplacementValue());
        }

        [Fact]
        public void TestGetImageReplacement_Pod()
        {
            const string image = "foobar";
            var patch = new JsonPatchDocument<V1Pod>();
            patch.Replace(p => p.Spec.Containers[10].Image, image);
            Assert.Equal(image, patch.TryGetContainerImageReplacementValue());

            patch.Operations.Clear();
            Assert.Null(patch.TryGetContainerImageReplacementValue());
        }
    }
}