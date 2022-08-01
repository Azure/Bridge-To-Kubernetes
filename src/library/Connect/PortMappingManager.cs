// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.Connect
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

        /// <summary>
        /// <see cref="IPortMappingManager.GetRemoteToFreeLocalPortMappings(IEnumerable{EndpointInfo})"/>
        /// </summary>
        public IEnumerable<EndpointInfo> GetRemoteToFreeLocalPortMappings(IEnumerable<EndpointInfo> endpoints)
        {
            var result = new List<EndpointInfo>();
            foreach (var endpoint in endpoints)
            {
                result.Add(this.MapToLocalFreePorts(endpoint));
            }
            return result;
        }

        public IDictionary<int, int> GetOccupiedWindowsPortsAndPids(IEnumerable<EndpointInfo> endpoints)
        {
            var localPorts = endpoints.SelectMany(e => e.Ports.Select(p => p.LocalPort)).Distinct();

            var testIp = IPAddress.Parse(IP.StartingIP);
            var occupiedPorts = localPorts.Where(p => !this.IsLocalPortAvailable(testIp, p));

            var portProcessIdsMappings = this.GetLocalPortToProcessIdMappings();
            return portProcessIdsMappings.Where(kvp => kvp.Key.IsIn(localPorts)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        /// <summary>
        /// <see cref="IPortMappingManager.IsLocalPortAvailable"/>
        /// </summary>
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

        #region private

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
                            throw new InvalidUsageException(_operationContext, Resources.CannotObtainFreeLocalPorts);
                        }
                    }
                    pp.LocalPort = _startPort;
                    _startPort++;
                }
            }
            return endpoint;
        }

        private IDictionary<int, int> GetLocalPortToProcessIdMappings()
        {
            var netstatCommand = "netstat";
            var netstatArgs = "-anop TCP";
            var (exitCode, output) = this._platform.ExecuteAndReturnOutput(netstatCommand,
                                                                           netstatArgs,
                                                                           timeout: TimeSpan.FromSeconds(30),
                                                                           stdOutCallback: (line) => _log.Info(line),
                                                                           stdErrCallback: (line) => _log.Error(line));
            if (exitCode != 0)
            {
                throw new UserVisibleException(_operationContext, Resources.CannotDetermineActiveConnectionsFormat, netstatArgs, exitCode, output);
            }

            return this._ParsePortToProcessMap(output);
        }

        /// <summary>
        /// Parses output of netstat command to get port to process ID map
        /// </summary>
        private IDictionary<int, int> _ParsePortToProcessMap(string output)
        {
            var result = new Dictionary<int, int>();
            using (StringReader reader = new StringReader(output))
            {
                int currentProcessId = -1;
                int currentPort = 0;
                while (true)
                {
                    var line = reader.ReadLine();

                    // Entire output has been processed
                    if (line == null)
                    {
                        break;
                    }

                    line = line.Trim();
                    var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && parts[0].StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                    {
                        // This is the TCP line, such as this:
                        //      TCP    0.0.0.0:80             0.0.0.0:0              LISTENING    1234
                        string localAddress = parts[1];
                        if (localAddress.StartsWith("0.0.0.0:", StringComparison.OrdinalIgnoreCase) ||
                            localAddress.StartsWith("[::]:", StringComparison.OrdinalIgnoreCase))
                        {
                            // Yes, this port is bound on all IPs.
                            if (int.TryParse(localAddress.Substring(localAddress.LastIndexOf(':') + 1), out int port) && port > 0 && port < 65536
                                && int.TryParse(parts.Last(), out int processId) && processId > 0)
                            {
                                currentPort = port;
                                currentProcessId = processId;
                            }
                        }
                    }

                    // Add the last port and process ID details that were read.
                    // If port details have been already added or not yet determined, currentPort value is 0.
                    if (currentPort > 0)
                    {
                        result[currentPort] = currentProcessId;
                        currentPort = 0;
                    }
                }
            }

            return result;
        }

        #endregion private
    }
}