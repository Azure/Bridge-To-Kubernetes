// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.EndpointManager.Logging;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// See <see cref="IHostsFileManager"/>
    /// </summary>
    internal class HostsFileManager : ServiceBase, IHostsFileManager
    {
        private static readonly string BRIDGE_HOSTS_HEADER = $"# Added by {Constants.Product.Name}";
        private const string BRIDGE_HOSTS_FOOTER = "# End of section";
        private readonly object _syncObject = new object();
        private readonly Lazy<string> _path;

        private readonly IFileSystem _fileSystem;
        private readonly IOperationContext _operationContext;
        private readonly ILog _log;

        public HostsFileManager(IPlatform platform, IFileSystem fileSystem, IOperationContext operationContext, ILog log)
        {
            _fileSystem = fileSystem;
            _operationContext = operationContext;
            _log = log;

            _path = new Lazy<string>(() =>
            {
                _log.Info("Getting hosts file path");
                string path;
                if (platform.IsWindows)
                {
                    path = _fileSystem.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                }
                else if (platform.IsOSX)
                {
                    path = "/private/etc/hosts";
                }
                else if (platform.IsLinux)
                {
                    path = "/etc/hosts";
                }
                else
                {
                    var errMessage = $"Unrecognized OS: '{RuntimeInformation.OSArchitecture}, {RuntimeInformation.OSDescription}'. Unable to locate hosts file";
                    _log.Error(errMessage);
                    throw new InvalidOperationException(errMessage);
                }
                if (!_fileSystem.FileExists(path))
                {
                    var errMessage = $"Hosts file could not be located at '{path}'";
                    _log.Error(errMessage);
                    throw new InvalidOperationException(errMessage);
                }
                _log.Info($"Located hosts file at {path}");
                return path;
            });
        }

        /// <summary>
        /// <see cref="IHostsFileManager.EnsureAccess"/>
        /// </summary>
        public void EnsureAccess()
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.EndpointManager.AreaName,
                    Events.EndpointManager.Operations.EnsureHostsFileAccess))
            {
                _log.Info("Checking for access to the hosts file...");
                try
                {
                    using (_fileSystem.OpenFileForWrite(_path.Value))
                    {
                        perfLogger.SetSucceeded();
                        _log.Info("Successfully accessed hosts file");
                    }
                }
                catch (Exception ex)
                {
                    _log.ExceptionAsWarning(ex);
                    var message = $"Can't access {_path.Value}. Please run command with admin privilege. Error: {ex.Message}";
                    _log.Error(message);
                    throw new InvalidUsageException(_operationContext, message);
                }
            }
        }

        /// <summary>
        /// <see cref="IHostsFileManager.Add"/>
        /// </summary>
        public void Add(string workloadNamespace, IEnumerable<HostsFileEntry> newEntries)
        {
            if (newEntries == null || !newEntries.Any())
            {
                _log.Warning("No hosts file entries were added.");
                return;
            }

            // TODO (lolodi): kept around to kep tests happy, this foreach can probably be removed as we always assign from an IPAddress (in fact the string in hostEntry should be IPAddress instead of string
            // The names are also populated from services already running in the cluster so no need to check them.
            foreach (var entry in newEntries)
            {
                // Validate IP
                if (!IPAddress.TryParse(entry.IP, out _))
                {
                    throw new InvalidOperationException($"Invalid IP address: {entry.IP}");
                }

                // Validate host name
                var baseDnsName = entry.Names.FirstOrDefault();
                if (string.IsNullOrEmpty(baseDnsName))
                {
                    throw new InvalidOperationException($"Invalid {nameof(HostsFileEntry)}: service name cannot be null");
                }
                if (Uri.CheckHostName(baseDnsName) == 0)
                {
                    throw new InvalidOperationException($"Invalid host name: {baseDnsName}");
                }
            }
            using (var perfLogger =
            _log.StartPerformanceLogger(
                Events.EndpointManager.AreaName,
                Events.EndpointManager.Operations.AddHostsFileEntries))
            {
                // Persist entries to the host file
                _log.Info($"Adding {newEntries.Count()} entries to hosts file...");
                lock (_syncObject)
                {
                    var fileContent = ReadFile();
                    var entries = ParseContent(fileContent);
                    entries.AddRange(newEntries);

                    WriteFile(fileContent, this.GetDistinctHostEntries(entries));
                }

                _log.Info($"Updated {newEntries.Count()} entries in the hosts file.");
                perfLogger.SetSucceeded();
            }
        }

        /// <summary>
        /// <see cref="IHostsFileManager.Remove"/>
        /// </summary>
        public void Remove(IEnumerable<IPAddress> ipAddresses)
        {
            lock (_syncObject)
            {
                var fileContent = ReadFile();
                var entries = ParseContent(fileContent);

                var count = entries.RemoveAll(e => ipAddresses.Select(ip => ip.ToString()).Contains(e.IP));

                WriteFile(fileContent, entries);

                _log.Info($"Removed {count} entries from the hosts file.");
            }
        }

        /// <summary>
        /// <see cref="IHostsFileManager.Clear"/>
        /// </summary>
        public void Clear()
        {
            lock (_syncObject)
            {
                WriteFile(ReadFile(), entries: new HostsFileEntry[] { });
            }

            _log.Info($"Removed all {Constants.Product.Name} entries from the hosts file.");
        }

        #region private members

        private string ReadFile()
        {
            _log.Info("Reading hosts file content");
            return _fileSystem.ReadAllTextFromFile(_path.Value);
        }

        private IEnumerable<HostsFileEntry> GetDistinctHostEntries(IEnumerable<HostsFileEntry> entries)
        {
            _log.Verbose("Filtering for distinct host entries");
            var reverseList = new List<HostsFileEntry>(entries);
            reverseList.Reverse();  // Reverse _hostEntries so the latest IP is used.

            var result = new List<HostsFileEntry>();

            foreach (var entry in reverseList)
            {
                if (result.Any(r => r.Names.Count == entry.Names.Count && r.Names.Intersect(entry.Names, StringComparer.OrdinalIgnoreCase).Count() == r.Names.Count))
                {
                    continue;
                }
                result.Add(entry);
            }
            return result;
        }

        private List<HostsFileEntry> ParseContent(string hostsFileContent)
        {
            _log.Info($"Getting {Constants.Product.Name} hosts file entries");
            var lpkLines = new List<string>();
            using (StringReader sr = new StringReader(hostsFileContent))
            {
                string line;
                bool sectionStarted = false;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrEmpty(line))
                    {
                        continue;
                    }
                    if (sectionStarted)
                    {
                        if (line.StartsWith(BRIDGE_HOSTS_FOOTER))
                        {
                            sectionStarted = false;
                            break;
                        }
                        if (line.StartsWith("#"))
                        {
                            continue;
                        }
                        lpkLines.Add(line);
                    }
                    else if (line.StartsWith(BRIDGE_HOSTS_HEADER))
                    {
                        sectionStarted = true;
                    }
                }
            }

            var lpkEntries = new List<HostsFileEntry>();
            foreach (var line in lpkLines)
            {
                var parts = line.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2)
                {
                    _log.Warning($"Bad line from hosts file: {line}, ignore.");
                    continue;
                }
                lpkEntries.Add(new HostsFileEntry()
                {
                    IP = parts[0],
                    Names = parts[1..parts.Length]
                });
            }

            _log.Info($"Found {lpkEntries.Count()} {Constants.Product.Name} entries in the hosts file");
            return lpkEntries;
        }

        private void WriteFile(string hostsFileContent, IEnumerable<HostsFileEntry> entries)
        {
            _log.Info($"Generating updated hosts file content with {entries.Count()} {Constants.Product.Name} entries");
            StringBuilder updatedFileContent = new StringBuilder();
            bool sectionStarted = false;
            using (StringReader sr = new StringReader(hostsFileContent))
            {
                string line;
                bool contentAdded = false;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith(BRIDGE_HOSTS_HEADER))
                    {
                        sectionStarted = true;
                        continue;
                    }
                    if (sectionStarted && line.StartsWith(BRIDGE_HOSTS_FOOTER))
                    {
                        sectionStarted = false;
                        contentAdded = true;
                        this.WriteHostsSection(updatedFileContent, entries);
                        continue;
                    }
                    if (!sectionStarted)
                    {
                        updatedFileContent.AppendLine(line);
                    }
                }
                if (!contentAdded)
                {
                    this.WriteHostsSection(updatedFileContent, entries);
                }
            }

            _log.Info("Writing hosts file content");
            _fileSystem.WriteAllTextToFile(_path.Value, updatedFileContent.ToString());
        }

        private void WriteHostsSection(StringBuilder content, IEnumerable<HostsFileEntry> entries)
        {
            if (entries.Any())
            {
                content.AppendLine(BRIDGE_HOSTS_HEADER);
                foreach (var entry in entries)
                {
                    content.AppendLine($"{entry.IP} {string.Join(" ", entry.Names)}");
                }
                content.AppendLine(BRIDGE_HOSTS_FOOTER);
            }
        }

        #endregion private members
    }
}