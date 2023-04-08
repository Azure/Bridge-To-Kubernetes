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

        [Fact]
        public void GetMacAddressHashOnLinux()
        {
            const string output = "        ether dc:41:a9:aa:1a:14  txqueuelen 1000  (Ethernet)\r\n        RX packets 0  bytes 0 (0.0 B)\r\n        RX errors 0  dropped 0  overruns 0  frame 0\r\n        TX packets 0  bytes 0 (0.0 B)\r\n        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0\r\n\r\ndummy0: flags=130<BROADCAST,NOARP>  mtu 1500\r\n        ether 6a:c7:29:b2:dc:9e  txqueuelen 1000  (Ethernet)\r\n        RX packets 0  bytes 0 (0.0 B)\r\n        RX errors 0  dropped 0  overruns 0  frame 0\r\n        TX packets 0  bytes 0 (0.0 B)\r\n        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0\r\n\r\neth0: flags=4163<UP,BROADCAST,RUNNING,MULTICAST>  mtu 1280\r\n        inet 0.0.0.0  netmask 0.0.0.0  broadcast 0.0.0.0\r\n        inet6 fe80::215:5dff:feb6:b81  prefixlen 64  scopeid 0x20<link>\r\n        ether 00:15:5d:b6:0b:81  txqueuelen 1000  (Ethernet)\r\n        RX packets 640092  bytes 605988996 (605.9 MB)\r\n        RX errors 0  dropped 0  overruns 0  frame 0\r\n        TX packets 77211  bytes 5949128 (5.9 MB)\r\n        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0\r\n\r\nlo: flags=73<UP,LOOPBACK,RUNNING>  mtu 65536\r\n        inet 127.0.0.1  netmask 255.0.0.0\r\n        inet6 ::1  prefixlen 128  scopeid 0x10<host>\r\n        loop  txqueuelen 1000  (Local Loopback)\r\n        RX packets 58608  bytes 361924804 (361.9 MB)\r\n        RX errors 0  dropped 0  overruns 0  frame 0\r\n        TX packets 58608  bytes 361924804 (361.9 MB)\r\n        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0\r\n\r\nsit0: flags=128<NOARP>  mtu 1480\r\n        sit  txqueuelen 1000  (IPv6-in-IPv4)\r\n        RX packets 0  bytes 0 (0.0 B)\r\n        RX errors 0  dropped 0  overruns 0  frame 0\r\n        TX packets 0  bytes 0 (0.0 B)\r\n        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0\r\n\r\ntunl0: flags=128<NOARP>  mtu 1480\r\n        tunnel   txqueuelen 1000  (IPIP Tunnel)\r\n        RX packets 0  bytes 0 (0.0 B)\r\n        RX errors 0  dropped 0  overruns 0  frame 0\r\n        TX packets 0  bytes 0 (0.0 B)\r\n        TX errors 0  dropped 0 overruns 0  carrier 0  collisions 0";

            var fakeClientConfig = A.Fake<IClientConfig>();
            _autoFake.Provide(fakeClientConfig);

            var fakePlatform = A.Fake<IPlatform>();
            A.CallTo(() => fakePlatform.IsLinux).Returns(true);
            A.CallTo(() => fakePlatform.ExecuteAndReturnOutput(A<string>.Ignored, A<string>.Ignored, A<TimeSpan>.Ignored, A<Action<string>>.Ignored, A<Action<string>>.Ignored, null, null)).Returns((0, output));
            _autoFake.Provide(fakePlatform);

            var macInformationProvider = _autoFake.Resolve<MacInformationProvider>();

            const string expectedResult = "e6e736e74149404e33e7f35171e3b178798c389a195b2fd81be1e9c0e6e13409";
            string result = macInformationProvider.GetMacAddressHash();

            Assert.Equal(expectedResult, result);

            A.CallTo(() => fakeClientConfig.SetProperty("mac.address", expectedResult)).MustHaveHappenedOnceExactly();
            A.CallTo(() => fakeClientConfig.Persist()).MustHaveHappenedOnceExactly();
        }
  }
}
