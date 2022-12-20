// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Tests
{
    public class LocalEnvironmentManagerTests : TestsBase
    {
        private ILocalEnvironmentManager _localEnvironmentManager;

        public LocalEnvironmentManagerTests()
        {
            var remoteContainerConnectionDetails = new AsyncLazy<RemoteContainerConnectionDetails>(async () => _autoFake.Resolve<RemoteContainerConnectionDetails>());
            _localEnvironmentManager = _autoFake.Resolve<LocalEnvironmentManager>(new NamedParameter("useKubernetesServiceEnvironmentVariables", true));
        }

        [Fact]
        public async void GetLocalEnvironment_GoodPath()
        {
            // Prepare test
            WorkloadInfo workloadInfo = new WorkloadInfo();
            Common.Models.EndpointInfo endpointInfo1 = new Common.Models.EndpointInfo();
            endpointInfo1.DnsName = Common.Constants.ManagedIdentity.TargetServiceNameOnLocalMachine;
            endpointInfo1.LocalIP = System.Net.IPAddress.Parse("127.0.0.1");
            endpointInfo1.Ports = new Common.Models.Settings.PortPair[] {new Common.Models.Settings.PortPair(5050, 80)};
            Common.Models.EndpointInfo endpointInfo2 = new Common.Models.EndpointInfo();
            endpointInfo2.DnsName = "foo";
            endpointInfo2.LocalIP = System.Net.IPAddress.Parse("127.0.0.2");
            endpointInfo2.Ports = new Common.Models.Settings.PortPair[] { new Common.Models.Settings.PortPair(5049, 80)};
            Common.Models.EndpointInfo endpointInfo3 = new Common.Models.EndpointInfo();
            endpointInfo3.DnsName = "kubernetes.default";
            endpointInfo3.LocalIP = System.Net.IPAddress.Parse("127.0.0.3");
            endpointInfo3.Ports = new Common.Models.Settings.PortPair[] { new Common.Models.Settings.PortPair(5048, 80)};

            workloadInfo.ReachableEndpoints = new List<Common.Models.EndpointInfo>{endpointInfo1, endpointInfo2, endpointInfo3};
            workloadInfo.EnvironmentVariables = new Dictionary<string, string>();

            // Execute
            IDictionary<string, string> result = _localEnvironmentManager.CreateEnvVariablesForK8s(workloadInfo);
            
            // Validate
            // We should add 8 entries per each service
            // Since one of the services is managed identity for bridge to kubernetes we should also add/update msi_enpoint variable
            Assert.Equal(8*3 + 1, result.Count());
            ValidateService("foo", result, "tcp", "5049", "127.0.0.2");
            ValidateService(Common.Constants.ManagedIdentity.TargetServiceNameOnLocalMachine, result, "tcp", "5050", "127.0.0.1");
            ValidateService("kubernetes", result, "tcp", "5048", "");
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[ManagedIdentity.MSI_ENDPOINT_EnvironmentVariable], "http://127.0.0.1:5050/metadata/identity/oauth2/token"));
            
        }

        public void ValidateService(string serviceName, IDictionary<string, string> result, string protocol, string port, string host) {
            var protocolUpper = protocol.ToUpperInvariant();
            serviceName = serviceName.ToUpperInvariant();
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_PORT"], $"{protocol}://{host}:{port}"));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_SERVICE_PORT_{protocolUpper}"], port.ToString()));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_PORT_{port}_{protocolUpper}_PROTO"], protocol));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_PORT_{port}_{protocolUpper}"], $"{protocol}://{host}:{port}"));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_PORT_{port}_{protocolUpper}_PORT"], port.ToString()));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_SERVICE_PORT"], port.ToString()));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_PORT_{port}_{protocolUpper}_ADDR"], host));
            Assert.True(StringComparer.OrdinalIgnoreCase.Equals(result[$"{serviceName}_SERVICE_HOST"], host));
        }

    }
}