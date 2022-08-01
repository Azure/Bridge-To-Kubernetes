// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.PortForward
{
    /// <summary>
    /// Implements the server side of port forwarding. It forwards the specified port from localhost or a service to
    /// a remote client. An instance manages all network streams to the forwarded port. Each stream is identified by a
    /// unique identifier.
    /// </summary>
    internal interface IServicePortForwardConnector
    {
        /// <summary>
        /// Establish port forwarding connection to the specified local port. A connection Id is returned, or -1 if the
        /// initial connection cannot be made.
        /// </summary>
        Task<int> ConnectAsync(Func<int, Task> connectHandler, Func<int, byte[], int, Task> receiveHandler, Func<int, Task> closeHandler);

        /// <summary>
        /// Send data from client to target. Returns false if data can't be sent.
        /// </summary>
        Task<bool> SendAsync(int id, byte[] data, CancellationToken cancellationToken);

        /// <summary>
        /// Stop the current port forwarding session, release all resources.
        /// </summary>
        void Stop();

        /// <summary>
        /// Disconnect a stream.
        /// </summary>
        void Disconnect(int id);
    }
}