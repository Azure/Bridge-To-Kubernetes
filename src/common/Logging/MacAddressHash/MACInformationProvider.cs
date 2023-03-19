// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.PersistentProperyBag;

namespace Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash
{
    /// <summary>
    /// This class provides information about the machine's Mac address
    /// </summary>
    internal class MacInformationProvider
    {
        private const string ZeroHash = "0000000000000000000000000000000000000000000000000000000000000000";
        private readonly IClientConfig _clientConfig;
        private readonly VSCodeStorageReader _vsCodeStorageReader;
        private readonly IPlatform _platform;
        private readonly IVSRegistryPropertyReader _vsRegistryPropertyReader;
        private readonly Lazy<string> _persistedMac;

        private const string MacAddressKey = "mac.address";
        private const string MacRegex = @"(?:[a-z0-9]{2}[:\-]){5}[a-z0-9]{2}";
        private const string ZeroRegex = @"(?:00[:\-]){5}00";
        private const string PersistRegex = @"[a-f0-9]{64}";

        private static class Commands
        {
            internal static class Windows
            {
                internal const string Command = "getmac";
            }

            internal static class Mono
            {
                internal const string Command = "ifconfig";
                internal const string Args = "-a";
            }
        }

        /// <summary></summary>
        /// <param name="clientConfig">The client config, it contains a cached value for the hashed mac</param>
        /// <param name="vsCodeStorageReader"></param>
        /// <param name="platform"></param>
        /// <param name="vsRegistryPropertyReader"></param>
        public MacInformationProvider(
            IClientConfig clientConfig,
            VSCodeStorageReader vsCodeStorageReader,
            IPlatform platform,
            IVSRegistryPropertyReader vsRegistryPropertyReader)
        {
            this._clientConfig = clientConfig;
            this._vsCodeStorageReader = vsCodeStorageReader;
            this._platform = platform;
            this._vsRegistryPropertyReader = vsRegistryPropertyReader;

            this._persistedMac = new Lazy<string>(() => this.GetMacAddressHash(), LazyThreadSafetyMode.ExecutionAndPublication);
        }

        public string MacAddressHash { get => this._persistedMac.Value; }

        /// <summary>
        /// Check if there is a persisted value otherwise calculates and persist a new one
        /// </summary>
        /// <returns>The hash of the mac address</returns>
        public string GetMacAddressHash()
        {
            string persistedValue = null;
            string result = null;

            // Check if VS already persisted a mac address hash in the registry
            if (this._platform.IsWindows)
            {
                persistedValue = this._vsRegistryPropertyReader.GetProperty(MacAddressKey) as string;

                if (ValidateMacAddressHash(persistedValue))
                {
                    result = persistedValue;
                    // Persist the value in the client config so, if VS get uninstalled, we keep our deviceid.
                    PersistMacAddressHash(result);
                    return result;
                }
            }

            // Check if VSCode is already persisting a mac address
            persistedValue = this._vsCodeStorageReader.MachineId;
            if (ValidateMacAddressHash(persistedValue))
            {
                result = persistedValue;
                // Persist the value in the client config so, if VSCode get uninstalled, we keep our deviceid.
                PersistMacAddressHash(result);
                return result;
            }

            // Check if the ClientConfig has the mac address hash
            persistedValue = this._clientConfig.GetProperty(MacAddressKey) as string;
            if (ValidateMacAddressHash(persistedValue))
            {
                return persistedValue;
            }

            // We don't have any persisted mac hash
            string computedHash = ComputeMacAddressHash();
            if (ValidateMacAddressHash(computedHash))
            {
                PersistMacAddressHash(computedHash);
                return computedHash;
            }

            // If anything fails return zero hash
            return ZeroHash;
        }

        private string ComputeMacAddressHash()
        {
            string macAddress = null;
            string hashedMacAddress = null;

            var data = this._platform.IsWindows ?
                RunCommandAndGetOutput(Commands.Windows.Command) :
                RunCommandAndGetOutput(Commands.Mono.Command, Commands.Mono.Args);

            if (!string.IsNullOrEmpty(data))
            {
                foreach (Match match in Regex.Matches(data, MacRegex, RegexOptions.IgnoreCase))
                {
                    if (!Regex.IsMatch(match.Value, ZeroRegex))
                    {
                        macAddress = match.Value;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(macAddress))
                {
                    //HashMacAddress
                    var hashInput = Encoding.UTF8.GetBytes(macAddress);
                    hashedMacAddress = BitConverter.ToString(FipsCompliantSha.Sha256.ComputeHash(hashInput)).Replace("-", string.Empty).ToLowerInvariant();
                }
            }

            return hashedMacAddress;
        }

        private void PersistMacAddressHash(string hashedMacAddress)
        {
            this._clientConfig.SetProperty(MacAddressKey, hashedMacAddress);
            this._clientConfig.Persist();
        }

        private static bool ValidateMacAddressHash(string macAddressHash)
            => !string.IsNullOrEmpty(macAddressHash) && Regex.IsMatch(macAddressHash, PersistRegex);

        private string RunCommandAndGetOutput(string commandName, string commandArgs = null)
        {
            try
            {
                (var exitCode, var output) = this._platform.ExecuteAndReturnOutput(commandName, 
                                                                              commandArgs, 
                                                                              timeout: TimeSpan.FromSeconds(30), 
                                                                              stdOutCallback: null,
                                                                              stdErrCallback: null);
                return output;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }
}