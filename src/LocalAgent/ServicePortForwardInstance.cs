// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.LocalAgent
{
    public class ServicePortForwardInstance
    {
        private readonly int _localPort;
        private readonly TcpListener _tcpListener;
        private const int BUFFER_SIZE = 40960;

        public ServicePortForwardInstance(int localPort)
        {
            _localPort = localPort;
            _tcpListener = new TcpListener(IPAddress.Parse("127.1.1.1"), _localPort);
            _tcpListener.Start();
        }

        public async Task RunAsync()
        {
            await Task.Yield();
            while (_tcpListener != null)
            {
                var localConnection = await _tcpListener.AcceptTcpClientAsync();
                if (_tcpListener == null)
                {
                    break;
                }
                Console.WriteLine($"Accept {_localPort}");
                this.RunLoopAsync(localConnection).Forget();
            }
        }

        private async Task RunLoopAsync(TcpClient connection)
        {
            await Task.Yield();
            try
            {
                var stream = connection.GetStream();
                int streamId = 0;

                byte[] buffer = new byte[BUFFER_SIZE];

                while (true)
                {
                    int cRead = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);
                    if (cRead == 0)
                    {
                        Console.WriteLine($"ServicePortForward: StopLocal {_localPort}");
                        connection.Dispose();
                        break;
                    }
                    else
                    {
                        Console.WriteLine($"ServicePortForward: received {cRead} bytes on port {_localPort}");
                        Console.WriteLine(Encoding.Default.GetString(buffer));
                    }
                }
            }
            catch (Exception ex) { }
        }
    }

    internal static class TaskExtensions
    {
        public static void Forget(this Task task)
        {
        }
    }
}