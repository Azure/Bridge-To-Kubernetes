// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.PersistentProperyBag
{
    /// <summary>
    ///     Persistant property bag on disk for machine storage.
    ///     To limit reads and writes the store is read in to memory upon creation and any values changed via set property
    ///     are only persisted to physical storage upon calling Persist().
    ///     This will need to handle shared access as multiple instances/processes may attempt concurrent read operations.
    ///     The Strategy is to minimize the reads and writes and avoid locking the file, allowing concurrent read/write operations.
    ///     A backup helps deal with any file corruption issues.
    ///     All applications should continue to function without this file as if it is corrupt or missing it will just be recreated
    ///     We are serializing the Dictionary to Json prior to persisting to disk to enable non .Net CLR applications to read the store.
    /// </summary>
    internal sealed class ClientConfig : IClientConfig
    {
        private readonly IFileSystem _fileSystem;

        private readonly string _backUpStorageLocation;
        private readonly string _storageLocation;
        private static readonly object _readWriteLock = new object();
        private ConcurrentDictionary<string, object> _store;
        private bool _storeWasChanged;

        public ClientConfig(IFileSystem fileSystem)
        {
            this._fileSystem = fileSystem;
            this._storageLocation = _fileSystem.Path.Combine(_fileSystem.GetPersistedFilesDirectory(DirectoryName.PersistedFiles), FileNames.Config);
            this._backUpStorageLocation = GetBackupStoreLocation(this._storageLocation);

            this.LoadStore();
        }

        internal static string GetBackupStoreLocation(string primaryLocation)
        {
            return primaryLocation + ".bak";
        }

        /// <summary>
        /// <see cref="IClientConfig.Persist"/>
        /// </summary>
        public void Persist()
        {
            // we do not want to write the file unless something has changed.
            if (!this._storeWasChanged)
            {
                return;
            }

            this._storeWasChanged = false;
            this.Persist(this._storageLocation);
            this.Persist(this._backUpStorageLocation);
        }

        /// <summary>
        /// <see cref="IClientConfig.Clear"/>
        /// </summary>
        public void Clear()
        {
            this._store.Clear();
            this._storeWasChanged = true;
        }

        /// <summary>
        /// <see cref="IClientConfig.GetAllProperties"/>
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> GetAllProperties()
        {
            return this._store.Select(kvp => new KeyValuePair<string, object>(kvp.Key, kvp.Value));
        }

        /// <summary>
        /// <see cref="IClientConfig.GetProperty(string)"/>
        /// </summary>
        public object GetProperty(string propertyName)
        {
            this._store.TryGetValue(propertyName, out var value);
            return value;
        }

        /// <summary>
        /// <see cref="IClientConfig.RemoveProperty(string)"/>
        /// </summary>
        public void RemoveProperty(string propertyName)
        {
            this._storeWasChanged = this._storeWasChanged || this._store.TryRemove(propertyName, out _);
        }

        /// <summary>
        /// <see cref="IClientConfig.SetProperty(string, int)"/>
        /// </summary>
        public void SetProperty(string propertyName, int value)
        {
            this.SetPropertyInternal(propertyName, value);
        }

        /// <summary>
        /// <see cref="IClientConfig.SetProperty(string, string)"/>
        /// </summary>
        public void SetProperty(string propertyName, string value)
        {
            this.SetPropertyInternal(propertyName, value);
        }

        private void Persist(string path)
        {
            try
            {
                lock (_readWriteLock)
                {
                    var dirPath = _fileSystem.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dirPath))
                    {
                        _fileSystem.CreateDirectory(dirPath);
                    }
                    _fileSystem.WriteAllTextToFile(path, JsonHelpers.SerializeObject(this._store));
                }
            }
            catch
            {
                // swallow exceptions.
            }
        }

        private void LoadStore()
        {
            // Attempt to parse primary store
            if (this.ParseIn(this._storageLocation))
            {
                if (!_fileSystem.FileExists(this._backUpStorageLocation))
                {
                    try
                    {
                        this.Persist(this._backUpStorageLocation);
                    }
                    catch
                    {
                        // swallow exceptions.
                    }
                }

                return;
            }

            // Failing primary store attempt the backup store
            if (this.ParseIn(this._backUpStorageLocation))
            {
                // if the backup was successfully parsed persist it as the primary.
                // There is rare but possible a race condition here where multiple processes may attempt to perform this action.
                // Therefore if we fail we will assume another process as succeeded and give up.
                try
                {
                    this.Persist(this._storageLocation);
                }
                catch
                {
                    // swallow exceptions.
                }

                return;
            }

            // if we failed to load either then we must create a new store.
            this._storeWasChanged = true;
            this._store = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        }

        private bool ParseIn(string filepath)
        {
            if (_fileSystem.FileExists(filepath))
            {
                try
                {
                    this._store = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                    lock (_readWriteLock)
                    {
                        var content = _fileSystem.ReadAllTextFromFile(filepath);
                        // Workaround needed because deserialization of ConcurrentDictioanry fails.
                        _store = new ConcurrentDictionary<string, object>(JsonHelpers.DeserializeObject<Dictionary<string, object>>(content));
                    }
                    // NewtonSoft.Json deserializes ints as longs.
                    foreach (var kvp in this._store.Where(kvp => kvp.Value is long).ToArray())
                    {
                        this._store[kvp.Key] = (int)(long)kvp.Value;
                    }

                    return true;
                }
                catch
                {
                    // if we fail for any reason we will recreate the store.
                }
            }

            return false;
        }

        private void SetPropertyInternal(string propertyName, object value)
        {
            object existingvalue;
            if (this._store.TryGetValue(propertyName, out existingvalue) && Equals(existingvalue, value))
            {
                // value is already the same as current value.
                return;
            }

            this._storeWasChanged = true;
            this._store[propertyName] = value;
        }
    }
}