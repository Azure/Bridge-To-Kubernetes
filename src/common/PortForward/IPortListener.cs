// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Sockets;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    /// <summary>
    /// Manager for port listening
    /// </summary>
    internal interface IPortListener : IDisposable

    {
        TcpListener Listener { get; }
    }
}