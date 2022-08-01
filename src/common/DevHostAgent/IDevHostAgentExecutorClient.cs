// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models.Channel;

namespace Microsoft.BridgeToKubernetes.Common.DevHostAgent
{
    internal interface IDevHostAgentExecutorClient : IDisposable
    {
        int LocalPort { get; }

        Task<bool> PingAsync(int timeoutMs, int retry, CancellationToken cancellationToken);

        Task ResetAsync(CancellationToken cancellationToken);

        Task ReversePortForwardStartAsync(PortForwardStartInfo port, Func<int, byte[], Task> dataHandler, Action<int> closedHandler, CancellationToken cancellationToken);

        Task ReversePortForwardSendAsync(int port, int streamId, byte[] content, CancellationToken cancellationToken);

        Task ReversePortForwardStopAsync(int port, int streamId, CancellationToken cancellationToken);

        Task<int> ServicePortForwardStartAsync(string serviceDns, int port, Func<byte[], Task> dataHandler, Action closedHandler, CancellationToken cancellationToken = default(CancellationToken));

        Task ServicePortForwardSendAsync(string serviceDns, int port, int streamId, byte[] content, CancellationToken cancellationToken);

        Task ServicePortForwardStopAsync(string serviceDns, int port, int streamId, CancellationToken cancellationToken);
    }
}