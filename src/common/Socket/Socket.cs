// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Socket
{
    /// <summary>
    /// <see cref="ISocket"/>
    /// </summary>
    internal class Socket : ISocket
    {
        private readonly System.Net.Sockets.Socket _socket;
        private readonly ILog _log;

        public Socket(ILog log)
        {
            _socket = new System.Net.Sockets.Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            {
                SendBufferSize = Constants.EndpointManager.SocketBufferSize,
                ReceiveBufferSize = Constants.EndpointManager.SocketBufferSize
            };
            _log = log;
        }

        private Socket(System.Net.Sockets.Socket socket, ILog log)
        {
            _socket = socket;
            _log = log;
        }

        public void Dispose()
        {
            _socket.Dispose();
            _log.Flush(timeout: TimeSpan.FromMilliseconds(1500));
        }

        public bool Connected
            => _socket.Connected;

        public async Task<ISocket> AcceptAsync()
            => new Socket(await _socket.AcceptAsync(), _log);

        public void Bind(string socketPath)
            => _socket.Bind(new UnixDomainSocketEndPoint(socketPath));

        public void Close()
            => _socket.Close();

        public void Listen()
            => _socket.Listen(backlog: 1);

        public Task ConnectAsync(EndPoint endpoint)
            => _socket.ConnectAsync(endpoint);

        public async Task<string> ReadUntilEndMarkerAsync()
        {
            byte[] bytes = new byte[_socket.ReceiveBufferSize];
            var messageBuilder = new StringBuilder();
            var numBytes = 0;
            var segment = string.Empty;
            do
            {
                numBytes = await _socket.ReceiveAsync(new ArraySegment<byte>(bytes), SocketFlags.None);
                segment = Encoding.UTF8.GetString(bytes[0..numBytes]);
                messageBuilder.Append(segment);
                _log.Info($"Received request segment: '{segment}' of size {numBytes}");
            }
            // Keep reading until entire message is received
            // If the socket closes (sometimes accidently, or because EPM crashed) the ReceiveAsync completes with 0 bytes (therefore no <EOF> terminator) and we are stuck in this loop.
            while (messageBuilder.ToString().IndexOf(Constants.EndpointManager.EndMarker) == -1 && numBytes > 0);

            var message = messageBuilder.ToString();
            return message.Replace(Constants.EndpointManager.EndMarker, "");
        }

        public async Task<int> SendWithEndMarkerAsync(string message)
        {
            var data = Encoding.UTF8.GetBytes(message + Constants.EndpointManager.EndMarker);
            var numBytes = await _socket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
            _log.Info($"{numBytes} bytes were sent.");
            return numBytes;
        }
    }
}