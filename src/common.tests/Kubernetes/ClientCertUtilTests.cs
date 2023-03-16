// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.TestHelpers;
using System.Security.Cryptography.X509Certificates;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Kubernetes
{
    public class ClientCertUtilTests : TestsBase
    {
        private const string KubeConfigFileName = "TestData/kubeconfig.yml";

        [Fact]
        public void GeneratePfxTest()
        {
            var cfg = KubernetesClientConfiguration.BuildConfigFromConfigFile(KubeConfigFileName, "victorian-context", useRelativePaths: false);

            var cert = ClientCertUtil.GeneratePfx(cfg);
            Assert.NotNull(cert.GetRSAPrivateKey());
        }

    }
}
