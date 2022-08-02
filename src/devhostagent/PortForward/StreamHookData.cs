// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.PortForward
{
    public class StreamHookData
    {
        internal static int _streamIdCtr = 0;

        public StreamHookData(TcpClient tcpClient, int id, Func<int, Task> connectHandler, Func<int, byte[], int, Task> dataHandler, Func<int, Task> closeHandler)
        {
            this.TcpClient = tcpClient ?? throw new ArgumentNullException(nameof(tcpClient));
            this.Id = id;
            this.ConnectHandler = connectHandler ?? throw new ArgumentNullException(nameof(connectHandler));
            this.DataHandler = dataHandler ?? throw new ArgumentNullException(nameof(dataHandler));
            this.CloseHandler = closeHandler ?? throw new ArgumentNullException(nameof(closeHandler));
        }

        public int Id { get; private set; }

        public TcpClient TcpClient { get; private set; }

        public Func<int, Task> ConnectHandler { get; private set; }

        public Func<int, byte[], int, Task> DataHandler { get; private set; }

        public Func<int, Task> CloseHandler { get; private set; }

        public void Stop()
        {
            try
            {
                this.CloseHandler?.Invoke(this.Id);
                this.TcpClient?.Dispose();
            }
            catch (Exception)
            {
            }

            this.TcpClient = null;
            this.ConnectHandler = null;
            this.DataHandler = null;
            this.CloseHandler = null;
        }
    }
}