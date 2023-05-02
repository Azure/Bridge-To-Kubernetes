// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using SystemTextJsonPatch;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.LocalAgent
{
    internal class PortMappingManager : IPortMappingManager
    {
        private readonly IPlatform _platform;
        private readonly ILog _log;
        private readonly IOperationContext _operationContext;
        private int _startPort = IP.RemoteServicesLocalStartingPort;

        public PortMappingManager(
            IPlatform platform,
            IOperationContext operationContext,
            ILog log)
        {
            this._platform = platform;
            this._operationContext = operationContext;
            this._log = log;
            if (!_platform.IsOSX && !_platform.IsLinux && !_platform.IsWindows)
            {
                throw new NotSupportedException("Unsupported platform");
            }
        }
        public IEnumerable<EndpointInfo> AddLocalPortMappings(IEnumerable<EndpointInfo> endpoints)
        {
            var result = new List<EndpointInfo>();
            foreach (var endpoint in endpoints)
            {
                result.Add(this.MapToLocalPorts(endpoint));
            }
            return result;
        }

        private EndpointInfo MapToLocalPorts(EndpointInfo endpoint)
        {
            if (_platform.IsOSX || _platform.IsLinux || StringComparer.OrdinalIgnoreCase.Equals(endpoint.DnsName, DAPR))
            {
                return MapToLocalFreePorts(endpoint);
            }

            // On Windows, there is no way to work around the port conflict problem as OSX did.
            // Using command such as
            // 'netsh interface portproxy add v4tov4 listenport=80 listenaddress=127.0.1.1 connectport=50080 connectaddress=127.0.1.1'
            // does not work because the portproxy is implemented as open a TCP port and forward,
            // not as a lower level filter as in OSX or Linux.

            // Windows
            foreach (var pp in endpoint.Ports)
            {
                pp.LocalPort = pp.RemotePort;
            }
            return endpoint;
        }

        // To support port conflict on OSX and Linux: If some program already allocated port X for
        // all IPs, no one else can bind and listen on the same port X. To avoid this problem,
        // we bind to ServiceRouterListenPortStart (55049) and use network filtering to forward
        // traffic to this port. See EndpointManager for the OSX & Linux implementations.
        private EndpointInfo MapToLocalFreePorts(EndpointInfo endpoint)
        {
            // When getting the elevation requests, the LocalIP is not yet defined, so we should use the loopback one
            // When actually assigning the ports, we should test with the real IP, so if multiple ports require mapping we test them with the same IP
            // withouth interfering with other endpoints already mapped to different IPs.
            var testIP = endpoint.LocalIP ?? IPAddress.Loopback;

            foreach (var pp in endpoint.Ports)
            {
                if (pp.LocalPort == Common.Constants.IP.PortPlaceHolder) // Assign ports only when not already specified
                {
                    while (!IsLocalPortAvailable(testIP, _startPort)) // Note at this point LocalIP should already be assigned by the IPManager and if on MacOS it hsould already be enabled.
                    {
                        _startPort++;
                        if (_startPort > IP.RemoteServicesLocalEndingPort)
                        {
                            throw new InvalidUsageException(_operationContext, "Cannot Obtain Local Ports");
                        }
                    }
                    pp.LocalPort = _startPort;
                    _startPort++;
                }
            }
            return endpoint;

        }

        public bool IsLocalPortAvailable(IPAddress localAddress, int port)
        {
            TcpListener tcpListener = null;
            try
            {
                tcpListener = new TcpListener(localAddress, port);
                tcpListener.Start();
                return true;
            }
            catch (Exception e) when (e is SocketException)
            {
                // expected error when port is taken. Ignore
            }
            catch (Exception e)
            {
                _log.ExceptionAsWarning(e);
            }
            finally
            {
                tcpListener?.Stop();
            }

            return false;
        }
    }
}