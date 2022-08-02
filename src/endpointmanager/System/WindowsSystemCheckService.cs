// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// <see cref="IWindowsSystemCheckService"/>
    /// </summary>
    internal class WindowsSystemCheckService : IWindowsSystemCheckService
    {
        private readonly IPlatform _platform;
        private readonly ILog _log;

        public WindowsSystemCheckService(IPlatform platform, ILog log)
        {
            _platform = platform;
            _log = log;
        }

        public EndpointManagerSystemCheckMessage RunCheck()
        {
            var serviceMessages = this.CheckForKnownServices();
            var (portMessages, portBindingMap) = this.CheckForKnownPorts();

            return new EndpointManagerSystemCheckMessage()
            {
                ServiceMessages = serviceMessages.Concat(portMessages).ToArray(),
                PortBinding = portBindingMap
            };
        }

        internal IEnumerable<SystemServiceCheckMessage> ParseForKnownService(string output)
        {
            var messages = new List<SystemServiceCheckMessage>();
            using (var reader = new StringReader(output))
            {
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    var serviceName = line.Trim();
                    int[] impactedPorts;
                    if (Common.Constants.EndpointManager.NonCriticalWindowsPortListeningServices.TryGetValue(serviceName, out impactedPorts))
                    {
                        messages.Add(new SystemServiceCheckMessage()
                        {
                            Ports = impactedPorts,
                            Message = string.Format(Resources.ServicePortInUseFormat, serviceName, string.Join(",", impactedPorts), Common.Constants.Product.Name)
                        });
                        _log.Warning($"Service {serviceName} is taking port {impactedPorts}");
                    }
                }
            }
            return messages;
        }

        internal IDictionary<int, string> ParseProcessPortMap(string output)
        {
            var result = new Dictionary<int, string>();
            using (var reader = new StringReader(output))
            {
                string processName = "";
                int currentPort = 0;
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        if (currentPort > 0)
                        {
                            result[currentPort] = processName;
                        }
                        break;
                    }
                    line = line.Trim();
                    var parts = line.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && parts[0].StartsWith("TCP", StringComparison.OrdinalIgnoreCase))
                    {
                        if (currentPort > 0)
                        {
                            result[currentPort] = processName;
                            currentPort = 0;
                        }
                        // This is the TCP line, such as this:
                        //      TCP    0.0.0.0:80             0.0.0.0:0              LISTENING    1234
                        string localAddress = parts[1];
                        if (localAddress.StartsWith("0.0.0.0:", StringComparison.OrdinalIgnoreCase) ||
                            localAddress.StartsWith("[::]:", StringComparison.OrdinalIgnoreCase))
                        {
                            // We can tell that this port is bound by some process or service on all IPs, so we won't be able to listen on this port regardless of which IP we choose.
                            // If we want to use this port, the service will need to be disabled.
                            int port, processId;
                            if (int.TryParse(localAddress.Substring(localAddress.LastIndexOf(':') + 1), out port) && port > 0 && port < 65536
                                && int.TryParse(parts.Last(), out processId) && processId > 0)
                            {
                                currentPort = port;
                                processName = $"PID {processId}";
                            }
                        }
                    }
                    else if (currentPort > 0)
                    {
                        processName = processName + " " + line;
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Run "net start" and look for known bad services.
        /// </summary>
        private IEnumerable<SystemServiceCheckMessage> CheckForKnownServices()
        {
            var (exitCode, output) = this.RunCommand("net", "start");

            if (exitCode != 0)
            {
                return new[]{ new SystemServiceCheckMessage()
                {
                    Ports = new int[] { },
                    Message = string.Format(Resources.CannotDetermineWindowsServicesFormat, Common.Constants.Product.Name, exitCode, output)
                }};
            }

            return this.ParseForKnownService(output);
        }

        private (IEnumerable<SystemServiceCheckMessage>, IDictionary<int, string>) CheckForKnownPorts()
        {
            var netstatArgs = "-abno -p TCP";
            var messages = new List<SystemServiceCheckMessage>();

            var (exitCode, output) = this.RunCommand("netstat", netstatArgs);

            if (exitCode != 0)
            {
                messages.Add(
                    new SystemServiceCheckMessage()
                    {
                        Ports = new int[] { },
                        Message = string.Format(Resources.CannotDetermineActiveConnections, netstatArgs, exitCode, output)
                    });

                return (messages, new Dictionary<int, string>());
            }

            return (messages, this.ParseProcessPortMap(output));
        }

        private (int, string) RunCommand(string command, string arguments)
        {
            var output = new StringBuilder();

            using (var process = _platform.CreateProcess(
                new ProcessStartInfo()
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                }))
            {
                process.OutputDataReceived += (sender, e) => { output.AppendLine(e.Data); };
                process.ErrorDataReceived += (sender, e) => { output.AppendLine(e.Data); };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    _log.Warning("'{0} {1}' failed with exit code {2}: {3}", command, arguments, process.ExitCode, output.ToString());
                }

                return (process.ExitCode, output.ToString());
            }
        }
    }
}