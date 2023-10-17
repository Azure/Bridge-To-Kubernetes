// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using FakeItEasy;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    /// <summary>
    /// Tests to ensure embedded files work as expected
    /// </summary>
    public class EmbeddedFileTests : TestsBase
    {
        private ImageProvider _imageProvider;
        private readonly IManagementClientFactory _managementClientFactory = A.Fake<IManagementClientFactory>();
        private readonly IKubeConfigManagementClient _kubeConfigManagementClient = A.Fake<IKubeConfigManagementClient>();
        private readonly IKubernetesManagementClient _kubernetesManagementClient = A.Fake<IKubernetesManagementClient>();

        public EmbeddedFileTests()
        {
            _managementClientFactory = _autoFake.Resolve<IManagementClientFactory>();
            _autoFake.Provide(_managementClientFactory);
            _autoFake.Provide(_kubeConfigManagementClient);
            _autoFake.Provide(_kubernetesManagementClient);
            A.CallTo(() => _managementClientFactory.CreateKubeConfigClient(A<string>.Ignored)).Returns(_kubeConfigManagementClient);
            A.CallTo(() => _kubeConfigManagementClient.GetKubeConfigDetails()).Returns(new KubeConfigDetails("", null, null, null, null));
            A.CallTo(() => _managementClientFactory.CreateKubernetesManagementClient(A<KubeConfigDetails>.Ignored)).Returns(_kubernetesManagementClient);
            A.CallTo(() => _kubernetesManagementClient.ListNodes(A<CancellationToken>._)).Returns(CreateV1NodeList());
            _imageProvider = _autoFake.Resolve<ImageProvider>();
        }

        [Fact]
        public void DevHostAgentImageTag() {
            ImageTagTest(() => _imageProvider.DevHostImage);
        }

        [Fact]
        public void RestorationJobImageTag()
            => ImageTagTest(() => _imageProvider.DevHostRestorationJobImage);

        [Fact]
        public void RoutingManagerImageTag()
            => ImageTagTest(() => _imageProvider.RoutingManagerImage);

        [Fact]
        public void DevHostAgentImageTagForArmArch() {
            ImageTagTest(() => _imageProvider.DevHostImage, true);
        }

        private static Task<OperationResponse<V1NodeList>> CreateV1NodeList()
        {
            var v1NodeList = new V1NodeList();
            var v1Node = new V1Node();
            var v1NodeStatus = new V1NodeStatus
            {
                NodeInfo = new V1NodeSystemInfo()
            };
            v1NodeStatus.NodeInfo.Architecture = Architecture.Arm64.ToString();
            v1Node.Status = v1NodeStatus;
            v1NodeList.Items = new System.Collections.Generic.List<V1Node>
            {
                v1Node
            };
            return Task.FromResult(new OperationResponse<V1NodeList>(v1NodeList, null));
        }

        private static void ImageTagTest(Func<string> tagProperty, bool isArmArch = false)
        {
            string image = tagProperty.Invoke();
            Assert.False(string.IsNullOrWhiteSpace(image));
            int i = image.IndexOf(':');
            Assert.NotEqual(-1, i);
            string tag = image.Substring(i + 1);
            Assert.False(string.IsNullOrWhiteSpace(tag));
            if (isArmArch) {
                Assert.Contains("-"+Architecture.Arm64.ToString(), image);
            }
        }
    }
}