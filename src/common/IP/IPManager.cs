// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Events = Microsoft.BridgeToKubernetes.Common.Logging.CommonEvents;

namespace Microsoft.BridgeToKubernetes.Common.IP
{
    internal class IPManager : IIPManager
    {
        private readonly ILog _log;
        private readonly IPlatform _platform;

        private readonly object _syncObject = new object();
        private readonly Dictionary<IPAddress, EndpointInfo> _endpoints = new Dictionary<IPAddress, EndpointInfo>();
        private IPAddress _previousIPAddress = IPAddress.Parse(Constants.IP.StartingIP);
        private readonly HashSet<string> _linuxRoutingRules = new HashSet<string>();

        public IPManager(IPlatform platform, ILog log)
        {
            _platform = platform;
            _log = log;
        }

        public IEnumerable<EndpointInfo> AllocateIPs(IEnumerable<EndpointInfo> endpoints, bool addRoutingRules, CancellationToken cancellationToken)
        {
            using (var perfLogger =
            _log.StartPerformanceLogger(
                Events.IP.AreaName,
                Events.IP.Operations.AllocateIP))
            {
                if (endpoints == null)
                {
                    return null;
                }
                foreach (var endpoint in endpoints)
                {
                    _log.Info("Allocating IP...");
                    lock (_syncObject)
                    {
                        if (endpoint.LocalIP == null)
                        {
                            var currentIP = _previousIPAddress.Next();
                            _previousIPAddress = currentIP;
                            this.EnableLocalIP(currentIP, cancellationToken);
                            endpoint.LocalIP = currentIP;
                            _log.Info($"Allocated IP {currentIP}");
                        }
                        else
                        {
                            // In case the LocalIP is already specified use the specified IP
                            this.EnableLocalIP(endpoint.LocalIP, cancellationToken);
                            _log.Info($"Allocated IP {endpoint.LocalIP}");
                        }
                    }
                }
                if (addRoutingRules)
                {
                    AddRoutingRules(endpoints, cancellationToken);
                }
                perfLogger.SetSucceeded();
                foreach (var endpoint in endpoints)
                {
                    _endpoints[endpoint.LocalIP] = endpoint;
                }
                return endpoints;
            }
        }

        public void Dispose()
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.IP.AreaName,
                    Events.IP.Operations.Cleanup))
            {
                _log.Info("Disposing...");
                var cancellationToken = new CancellationToken();

                _log.Info("Removing IP allocation...");
                foreach (var endpoint in _endpoints.Values)
                {
                    this.UndoEnableLocalIP(endpoint.LocalIP, cancellationToken);
                }
                _endpoints.Clear();

                _log.Info("Removing routing rules...");
                RemoveRoutingRules(cancellationToken: cancellationToken);

                _log.Info("Dispose complete.");
                perfLogger.SetSucceeded();
            }
        }

        public void FreeIPs(IPAddress[] ipsToCollect, IHostsFileManager hostsFileManager, bool removeRoutingRules, CancellationToken cancellationToken)
        {
            if (ipsToCollect == null || !ipsToCollect.Any())
            {
                _log.Warning("No IPs were specified, so none will be freed");
                return;
            }

            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.IP.AreaName,
                    Events.IP.Operations.ReleaseIP))
            {
                var freedCount = 0;
                lock (_syncObject)
                {
                    if (hostsFileManager != null)
                    {
                        hostsFileManager.Remove(ipsToCollect);
                    }

                    foreach (var address in ipsToCollect)
                    {
                        if (_endpoints.TryGetValue(address, out var endpoint) && _endpoints.Remove(address))
                        {
                            this.UndoEnableLocalIP(endpoint.LocalIP, cancellationToken);
                            freedCount++;
                        }
                    }
                }
                if (removeRoutingRules)
                {
                    RemoveRoutingRules(cancellationToken, ipsToCollect);
                }
                _log.Info($"{freedCount} IPs were freed");
                perfLogger.SetSucceeded();
            }
        }

        #region private members

        private void AddRoutingRules(IEnumerable<EndpointInfo> endpoints, CancellationToken cancellationToken)
        {
            if (_platform.IsWindows)
            {
                return;
            }

            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.IP.AreaName,
                    Events.IP.Operations.AddRoutingRules))
            {
                if (_platform.IsOSX)
                {
                    // TODO: Fix Bug 1125798: Need to be a good pf citizen
                    StringBuilder inputBuilder = new StringBuilder();

                    foreach (var endpoint in endpoints)
                    {
                        if (endpoint.LocalIP.Equals(IPAddress.Loopback))
                        {
                            continue;
                        }
                        foreach (var portPair in endpoint.Ports)
                        {
                            inputBuilder.AppendLine($"rdr pass inet proto tcp from any to {endpoint.LocalIP} port {portPair.RemotePort} -> {endpoint.LocalIP} port {portPair.LocalPort}");
                        }
                    }

                    lock (_syncObject)
                    {
                        _log.Info("Updating routing table...");
                        (var exitCode, var output) = _platform.ExecuteAndReturnOutput(
                            command: "/sbin/pfctl",
                            arguments: "-ef -",
                            timeout: TimeSpan.FromSeconds(10),
                            stdOutCallback: (line) => _log.Verbose(line),
                            stdErrCallback: (line) => _log.Error(line),
                            processInput: inputBuilder.ToString());

                        var resultMessage = $"'/sbin/pfctl -ef - {inputBuilder}' returned exit code {exitCode}";
                        if (exitCode != 0)
                        {
                            _log.Warning(resultMessage);
                        }
                        else
                        {
                            _log.Info(resultMessage);
                        }
                        _log.Verbose($"All output: {output}");
                        perfLogger.SetSucceeded();
                    }
                }
                else if (_platform.IsLinux)
                {
                    var rules = new List<string>();
                    foreach (var endpoint in endpoints)
                    {
                        foreach (var portPair in endpoint.Ports)
                        {
                            // --wait        -w [seconds]    maximum wait to acquire xtables lock before give up
                            rules.Add($"--table nat --append PREROUTING -p tcp --dst {endpoint.LocalIP} --dport {portPair.RemotePort} --jump DNAT --to-destination {endpoint.LocalIP}:{portPair.LocalPort} --wait 30");
                            rules.Add($"--table nat --append OUTPUT -p tcp --dst {endpoint.LocalIP} --dport {portPair.RemotePort} --jump DNAT --to-destination {endpoint.LocalIP}:{portPair.LocalPort} --wait 30");
                        }
                    }

                    lock (_syncObject)
                    {
                        _log.Info("Adding rules to routing table...");
                        foreach (var rule in rules)
                        {
                            if (_linuxRoutingRules.Add(rule))
                            {
                                (var exitCode, var output) = RunUtility("iptables", rule, cancellationToken); // Only apply the rule if it wasn't previously present in the ruleset
                                if (exitCode != 0)
                                {
                                    throw new InvalidUsageException(_log.OperationContext, $"Running 'iptables' failed with exit code '{exitCode}': '{output}'");
                                }
                            }
                        }
                        perfLogger.SetSucceeded();
                    }
                }
                else
                {
                    throw new NotSupportedException("Unsupported platform");
                }
            }
        }

        private void RemoveRoutingRules(CancellationToken cancellationToken, IPAddress[] ipsToCollect = null)
        {
            if (_platform.IsWindows)
            {
                return;
            }

            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.IP.AreaName,
                    Events.IP.Operations.RemoveRoutingRules))
            {
                if (_platform.IsOSX)
                {
                    // TODO: Fix Bug 1125798: Need to be a good pf citizen
                    // This is where we should restore the user's previous pf settings.
                }
                else if (_platform.IsLinux)
                {
                    lock (_syncObject)
                    {
                        _log.Info("Removing rules from routing table...");
                        if (ipsToCollect != null)
                        {
                            // If some IPs were passed, only clean up those specific ones.
                            foreach (var ip in ipsToCollect)
                            {
                                var command = _linuxRoutingRules.FirstOrDefault(a => a.Contains(ip.ToString()));
                                if (command != null && _linuxRoutingRules.Remove(command))
                                {
                                    RunUtility("iptables", command.Replace("--append", "--delete"), cancellationToken);
                                }
                            }
                        }
                        else
                        {
                            // If no argument was passed, we clear ALL rules.
                            foreach (var command in _linuxRoutingRules)
                            {
                                RunUtility("iptables", command.Replace("--append", "--delete"), cancellationToken);
                            }
                            _linuxRoutingRules.Clear();
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("Unsupported platform");
                }
                perfLogger.SetSucceeded();
            }
        }

        #region networking utils

        private (int, string) RunUtility(string executable, string args, CancellationToken cancellationToken)
        {
            _log.Info($"Running '{executable} {args}'");
            int exitCode = _platform.Execute(
                executable: executable,
                command: args,
                logCallback: (line) => _log.Info(line),
                envVariables: null,
                timeout: TimeSpan.FromSeconds(60), // increasing the timeout to match with iptables --wait time.
                cancellationToken: cancellationToken,
                out string output);

            if (exitCode != 0)
            {
                _log.Warning($"'{executable} {args}' failed with exit code '{exitCode}'.");
            }
            return (exitCode, output);
        }

        private void EnableLocalIP(IPAddress address, CancellationToken cancellationToken)
        {
            if (_platform.IsOSX && !address.Equals(IPAddress.Loopback)) // Leave the loopback IP alone
            {
                _log.Verbose($"Enabling local IP {address}");

                // for OSX, needs to run this command to create an alias:
                //  ifconfig lo0 alias <IP>
                // and at clean up
                //  ifconfig lo0 -alias <IP>
                this.RunUtility("/sbin/ifconfig", $"lo0 alias {address} netmask 255.255.255.255", cancellationToken);
            }
        }

        private void UndoEnableLocalIP(IPAddress address, CancellationToken cancellationToken)
        {
            if (_platform.IsOSX && !address.Equals(IPAddress.Loopback)) // Leave the loopback IP alone
            {
                _log.Verbose($"Disabling local IP {address}");

                // for OSX, needs to run this command to create an alias:
                //  ifconfig lo0 alias <IP>
                // and at clean up
                //  ifconfig lo0 -alias <IP>
                this.RunUtility("/sbin/ifconfig", $"lo0 -alias {address}", cancellationToken);
            }
        }

        #endregion networking utils

        #endregion private members
    }
}