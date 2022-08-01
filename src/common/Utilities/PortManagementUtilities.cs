// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal static class PortManagementUtilities
    {
        public static int GetAvailableLocalPort()
        {
            TcpListener l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }
    }
}
