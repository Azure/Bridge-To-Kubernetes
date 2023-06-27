// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;

namespace Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash
{
    internal class VSCodeStorageReader
    {
        private const string _windowsPath = "C:\\Users\\{0}\\AppData\\Roaming\\Code\\storage.json";
        private const string _linuxPath = "/home/{0}/.config/Code/storage.json";
        private const string _osxPath = "/Users/{0}/Library/Application Support/Code/storage.json";

        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly IPlatform _platform;

        public VSCodeStorageReader(
            IFileSystem fileSystem,
            IEnvironmentVariables environmentVariables,
            IPlatform platform)
        {
            this._fileSystem = fileSystem;
            this._environmentVariables = environmentVariables;
            this._platform = platform;
        }

        public string MachineId { get => GetProperty("telemetry.machineId") as string; }

        private object GetProperty(string name)
        {
            string vsCodeStoragePath = null;

            if (this._platform.IsWindows)
            {
                vsCodeStoragePath = _windowsPath;
            }
            else if (this._platform.IsOSX)
            {
                vsCodeStoragePath = _osxPath;
            }
            else if (this._platform.IsLinux)
            {
                vsCodeStoragePath = _linuxPath;
            }

            try
            {
                vsCodeStoragePath = string.Format(vsCodeStoragePath, this._environmentVariables.UserName);

                if (!this._fileSystem.FileExists(vsCodeStoragePath))
                {
                    return null;
                }

                var serializedStorage = this._fileSystem.ReadAllTextFromFile(vsCodeStoragePath);
                dynamic deserializedStorage = JsonHelpers.DeserializeObject<object>(serializedStorage);
                return deserializedStorage[name].Value;
            }
            catch { }

            return null;
        }
    }
}