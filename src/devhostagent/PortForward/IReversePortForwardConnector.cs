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
    /// Implements the server side of reverse port forwarding. A reverse port forwarder works the opposite of
    /// <see cref="IServicePortForwardConnector"/>. It forwards a port running on the remote client to localhost.
    /// An instance manages all network streams to the forwarded port. Each stream is identified by a unique identifier.
    /// </summary>
    internal interface IReversePortForwardConnector
    {
        /// <summary>
        /// Start reverse port forwarding. Returns false if the reverse port forwarder can't be started, for example,
        /// the port is already in use.
        /// </summary>
        Task<bool> ConnectAsync(Func<int, Task> connectHandler, Func<int, byte[], int, Task> receiveHandler, Func<int, Task> closeHandler);

        /// <summary>
        /// Send data from remote client to local server
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

        /// <summary>
        /// Checks whether incoming request is a probe request
        /// </summary>
        bool IsProbeRequest(int id, byte[] buffer, int length, string probe);

        /// <summary>
        /// Respond to a stream with OK HTTP response message
        /// </summary>
        Task AnswerWithHttpOkAsync(int id);
    }
}