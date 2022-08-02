// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    internal class PortListener : IPortListener
    {
        private readonly ILog _log;
        private int _localPort;
        private CancellationTokenRegistration _ctr = default(CancellationTokenRegistration);
        private bool _disposed = false;

        public TcpListener Listener { get; private set; }

        internal delegate IPortListener Factory(int localPort, CancellationToken cancellationToken);

        public PortListener(int localPort, CancellationToken cancellationToken, ILog log)
        {
            this._localPort = localPort;
            this._log = log ?? throw new ArgumentNullException(nameof(log));

            Listener = new TcpListener(IPAddress.Loopback, _localPort);
            _log.Verbose($"PortListener created on {_localPort}");

            _ctr = cancellationToken.Register(() => this.Dispose());

            Listener.Start(512);
            _log.Verbose($"PortListener started on {_localPort}");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;
            _log.Verbose($"PortListener stopped on {_localPort}");
            Listener.Stop();
            _ctr.Dispose();
        }
    }
}