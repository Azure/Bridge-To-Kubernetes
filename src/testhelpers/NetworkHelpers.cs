// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net;
using System.Net.Sockets;

namespace Microsoft.BridgeToKubernetes.TestHelpers
{
    public static class NetworkHelpers
    {
        /// <summary>
        /// Gets a random unused TCP port on the current machine
        /// </summary>
        /// <returns></returns>
        public static int GetRandomUnusedPort()
        {
            // Find a free port
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}