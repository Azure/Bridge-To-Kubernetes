// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;

namespace Microsoft.BridgeToKubernetes.Library.EndpointManagement
{
    /// <summary>
    /// A client for communicating with the EndpointManager
    /// </summary>
    public interface IEndpointManagementClient : IDisposable
    {
        /// <summary>
        /// Instructs EndpointManager which entries to add to the hosts file on the machine
        /// </summary>
        Task AddHostsFileEntryAsync(string workloadNamespace, IEnumerable<HostsFileEntry> hostsFileEntries, CancellationToken cancellationToken);

        /// <summary>
        /// Gets IPs to use when binding to the specified ports locally
        /// </summary>
        Task<IEnumerable<EndpointInfo>> AllocateIPAsync(IEnumerable<EndpointInfo> endpoints, CancellationToken cancellationToken);

        /// <summary>
        /// Release an IP previously in use
        /// </summary>
        Task FreeIPAsync(IPAddress[] ipsToCollect, CancellationToken cancellationToken);

        /// <summary>
        /// Disable/kill services and process occupying ports needed by the current operation
        /// </summary>
        Task FreePortsAsync(IEnumerable<IElevationRequest> elevationRequests, CancellationToken cancellationToken);

        /// <summary>
        /// Pings the EndpointManager. Returns true for a successful response
        /// </summary>
        Task<bool> PingEndpointManagerAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Invokes the EndpointManager. Throws if unsuccessful.
        /// </summary>
        Task StartEndpointManagerAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Checks the system for known services or processes that may inhibit portforwarding
        /// </summary>
        Task<EndpointManagerSystemCheckMessage> SystemCheckAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gives the EndpointManager the order to tear itself down
        /// </summary>
        /// <throws>An <see cref="System.InvalidOperationException"/> if the EPM is not successfully stopped</throws>
        Task StopEndpointManagerAsync(CancellationToken cancellationToken);
    }
}