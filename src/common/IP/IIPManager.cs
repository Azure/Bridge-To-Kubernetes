// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Models;

namespace Microsoft.BridgeToKubernetes.Common.IP
{
    internal interface IIPManager : IDisposable
    {
        IEnumerable<EndpointInfo> AllocateIPs(IEnumerable<EndpointInfo> endpoints, bool addRoutingRules, CancellationToken cancellationToken);

        void FreeIPs(IPAddress[] ipsToCollect, IHostsFileManager hostsFileManager, bool removeRoutingRules, CancellationToken cancellationToken);
    }
}