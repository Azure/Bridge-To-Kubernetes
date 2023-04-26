// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Exceptions;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager
{
    /// <summary>
    /// See <see cref="IHostsFileManager"/>
    /// </summary>
    internal interface IHostsFileManager
    {
        /// <summary>
        /// Checks for access to the 'hosts' file by attempting to open it for writing
        /// </summary>
        /// <exception cref="InvalidUsageException">If the file cannot be opened for writing</exception>
        void EnsureAccess();

        /// <summary>
        /// Adds <paramref name="newEntries"/> to the 'hosts' file
        /// </summary>
        void Add(string workloadNamespace, IEnumerable<HostsFileEntry> newEntries);

        /// <summary>
        /// Removes entries for <paramref name="ipAddresses"/> from the 'hosts' file
        /// </summary>
        void Remove(IEnumerable<IPAddress> ipAddresses);

        /// <summary>
        /// Clears all LPK entries from the 'hosts' file
        /// </summary>
        void Clear();
    }
}