using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash;
using Microsoft.BridgeToKubernetes.Common.PersistentProperyBag;
using Microsoft.BridgeToKubernetes.TestHelpers;
using System;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Logging.MacAddressHash
{
    public class MacInformationProviderTests : TestsBase
    {
        [Fact]
        public void GetMacAddressHashOnWindows()
        {
            const string output = "Physical Address    Transport Name\r\n=================== ==========================================================\r\nDC-41-A9-AA-1A-14   Media disconnected\r\nDC-42-A9-AA-1E-18   Media disconnected\r\nCA-48-3A-C0-A6-63   \\Device\\Tcpip_{DCA2D11A-367A-4582-A3C5-077619A50152}";

            var fakeClientConfig = A.Fake<IClientConfig>();
            _autoFake.Provide(fakeClientConfig);

            var fakePlatform = A.Fake<IPlatform>();
            A.CallTo(() => fakePlatform.IsWindows).Returns(true);
            A.CallTo(() => fakePlatform.ExecuteAndReturnOutput(A<string>.Ignored, A<string>.Ignored, A<TimeSpan>.Ignored, A<Action<string>>.Ignored, A<Action<string>>.Ignored, null, null)).Returns((0, output));
            _autoFake.Provide(fakePlatform);

            var macInformationProvider = _autoFake.Resolve<MacInformationProvider>();

            const string expectedResult = "f52b35d47f8b2bf2eb37182c7dd6197d1879b90cb43f80f3eeda7a4b77eb1fd9";
            string result = macInformationProvider.GetMacAddressHash();

            Assert.Equal(expectedResult, result);

            A.CallTo(() => fakeClientConfig.SetProperty("mac.address", expectedResult)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeClientConfig.Persist()).MustHaveHappenedOnceExactly();
        }
    }
}
