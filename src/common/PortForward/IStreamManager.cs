// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    /// <summary>
    /// Used to start network streams between a remote pod and the localhost
    /// </summary>
    internal interface IStreamManager
    {
        /// <summary>
        /// Starts a stream. Traffic between the provided localhost client and remote pod (via WebSocket factory), will be ferried back and forth.
        /// </summary>
        void Start(TcpClient localConnection, Func<Task<WebSocket>> remoteConnectionFactory, int remotePort, string logMessagePrefixFormat, object[] logMessagePrefixArgs, ILog log, CancellationToken cancellationToken);
    }
}