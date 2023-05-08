// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Socket
{
    /// <summary>
    /// Wraps <see cref="System.Net.Sockets.Socket"/> to enable unit testing
    /// </summary>
    internal interface ISocket : IDisposable
    {
        /// <summary>
        /// <see cref="System.Net.Sockets.Socket.Connected"/>
        /// </summary>
        bool Connected { get; }

        /// <summary>
        /// <see cref="System.Net.Sockets.Socket.AcceptAsync()"/>
        /// </summary>
        Task<ISocket> AcceptAsync();

        /// <summary>
        /// <see cref="System.Net.Sockets.Socket.Bind"/>
        /// </summary>
        void Bind(string socketPath);

        /// <summary>
        /// <see cref="System.Net.Sockets.Socket.Close()"/>
        /// </summary>
        void Close();

        /// <summary>
        /// <see cref="System.Net.Sockets.Socket.Listen()"/>
        /// </summary>
        void Listen();

        /// <summary>
        /// <see cref="System.Net.Sockets.SocketTaskExtensions.ConnectAsync(System.Net.Sockets.Socket, EndPoint)"/>
        /// </summary>
        Task ConnectAsync(EndPoint endpoint);

        /// <summary>
        /// Keeps reading from the socket until the end marker is detected
        /// </summary>
        /// <returns></returns>
        Task<string> ReadUntilEndMarkerAsync();

        /// <summary>
        /// Appends an end marker to the given message and sends it over the socket
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        Task<int> SendWithEndMarkerAsync(string message);
    }
}