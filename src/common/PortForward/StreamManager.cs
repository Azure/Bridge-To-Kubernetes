// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.PortForward
{
    internal class StreamManager : IStreamManager
    {
        /// <summary>
        /// <see cref="IStreamManager.Start(TcpClient, Func{Task{WebSocket}}, int, string, object[], ILog, CancellationToken)"/>
        /// </summary>
        public void Start(TcpClient localConnection, Func<Task<WebSocket>> remoteConnectionFactory, int remotePort, string logMessagePrefixFormat, object[] logMessagePrefixArgs, ILog log, CancellationToken cancellationToken)
        {
            var stream = new StreamInstance(localConnection, remoteConnectionFactory, remotePort, logMessagePrefixFormat, logMessagePrefixArgs, log);
            stream.Run(cancellationToken).Forget();
        }

        /// <summary>
        /// Private implementation class
        /// </summary>
        private class StreamInstance
        {
            private TcpClient _localConnection;
            private Func<Task<WebSocket>> _remoteConnectionFactory;
            private ILog _log;
            private string _logMessagePrefixFormat;
            private object[] _logMessagePrefixArgs;
            private int _remotePort;

            private NetworkStream _localStream;
            private WebSocket _remote;
            private StreamDemuxer _remoteStreams;
            private bool _stop;
            private SemaphoreSlim _syncObject = new SemaphoreSlim(1);
            private bool _receiveStarted;
            private int _remoteStartRetryCount = 0;

            private const int BUFFER_SIZE = 81920;

            public StreamInstance(TcpClient localConnection, Func<Task<WebSocket>> remoteConnectionFactory, int remotePort, string logMessagePrefixFormat, object[] logMessagePrefixArgs, ILog log)
            {
                _localConnection = localConnection ?? throw new ArgumentNullException(nameof(localConnection));
                _remoteConnectionFactory = remoteConnectionFactory ?? throw new ArgumentNullException(nameof(remoteConnectionFactory));
                _log = log ?? throw new ArgumentNullException(nameof(log));
                _logMessagePrefixFormat = logMessagePrefixFormat ?? throw new ArgumentNullException(nameof(logMessagePrefixFormat));
                _logMessagePrefixFormat = $"{_logMessagePrefixFormat} : {{{logMessagePrefixArgs?.Length ?? 0}}}";
                _logMessagePrefixArgs = logMessagePrefixArgs;
                _localStream = localConnection.GetStream();
                _remotePort = remotePort;
            }

            public Task Run(CancellationToken cancellationToken)
            {
                using (cancellationToken.Register(() => Stop()))
                {
                    return RunSendLoop();
                }
            }

            private async Task RunSendLoop()
            {
                LogVerboseMessage("Run send loop");

                byte[] buffer = new byte[BUFFER_SIZE];
                while (true)
                {
                    int cRead = _localStream != null ? await _localStream.ReadAsync(buffer, 0, buffer.Length) : 0;
                    if (cRead == 0)
                    {
                        LogVerboseMessage("Local stream finished. Close");
                        this.Stop();
                        break;
                    }
                    else
                    {
                        bool sendSuccess = false;
                        try
                        {
                            await EnsureRemoteStartAsync();
                            var s = this.GetRemoteStream(_remoteStreams, forWrite: true);
                            await s.WriteAsync(buffer, 0, cRead);
                            sendSuccess = true;
                        }
                        catch (Exception ex)
                        {
                            LogVerboseMessage($"Write to remote failed with {ex.Message}");
                            this.StopRemote();
                        }
                        if (!sendSuccess)
                        {
                            LogVerboseMessage("Send not successful. Close");
                            this.Stop();
                            break;
                        }
                    }
                }
            }

            private async Task RunReceiveLoop()
            {
                LogVerboseMessage("Run receive loop");
                byte[] buffer = new byte[BUFFER_SIZE];
                var s = this.GetRemoteStream(_remoteStreams, forRead: true);
                _receiveStarted = true;
                long bytesReceived = 0;
                while (true)
                {
                    try
                    {
                        int cRead = await s.ReadAsync(buffer, 0, buffer.Length);
                        if (cRead == 0)
                        {
                            LogVerboseMessage("Remote stream finished. Closed");
                            this.Stop();
                            break;
                        }
                        else
                        {
                            if (_localStream != null)
                            {
                                bytesReceived += cRead;
                                if (bytesReceived == 2 && cRead == 2 && (_remotePort >> 8) == buffer[1] && (_remotePort % 256) == buffer[0])
                                {
                                    // This is a bug in the K8s client library around port-forwarding. Some times at the first receiving, K8s will send
                                    // back the port number in the first 2 bytes. K8s client library should filter out these 2 bytes but it didn't.
                                    // Work around this issue here before the K8s client library fix. https://github.com/kubernetes-client/csharp/issues/229
                                    continue;
                                }
                                await _localStream.WriteAsync(buffer, 0, cRead);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        LogVerboseMessage($"Write to local failed with {ex.Message}");
                        this.Stop();
                        break;
                    }
                }
            }

            private async Task EnsureRemoteStartAsync()
            {
                if (_remote != null)
                {
                    return;
                }
                StreamDemuxer remoteStreams = null;
                await _syncObject.WaitAsync();
                try
                {
                    if (_remote == null)
                    {
                        _remote = await _remoteConnectionFactory();
                        _remoteStreams = new StreamDemuxer(_remote);
                        _remoteStreams.ConnectionClosed += this.RemoteConnectionClosed;
                        remoteStreams = _remoteStreams;
                    }
                }
                finally
                {
                    _syncObject.Release();
                }
                if (remoteStreams != null)
                {
                    remoteStreams.Start();
                    Task.Run(() => this.RunReceiveLoop()).Forget(); // Start remote loop
                }
            }

            private void RemoteConnectionClosed(object sender, EventArgs e)
            {
                LogVerboseMessage("RemoteConnection closed.");
                if (!_stop)
                {
                    if (!_receiveStarted && _remoteStartRetryCount == 0)
                    {
                        _remoteStartRetryCount++;
                        this.StopRemote();

                        // There is a bug still under investigation: some times when a port-forwarding connection was made via K8s client SDK,
                        // it will close the connection immediately at the web socket. This code is specific to detect this code and try to
                        // re-create the underlying WebSocket.
                        _remote = _remoteConnectionFactory().Result;
                        _remoteStreams = new StreamDemuxer(_remote);
                        _remoteStreams.ConnectionClosed += this.RemoteConnectionClosed;
                        _remoteStreams.Start();
                    }
                    else
                    {
                        this.Stop();
                    }
                }
            }

            private Stream GetRemoteStream(StreamDemuxer remoteStreams, bool forRead = false, bool forWrite = false)
            {
                // We need this lock to get around a race condition in the SDK.
                // We won't need this when we upgrade to use the latest verison of the SDK.
                lock (remoteStreams)
                {
                    return remoteStreams.GetStream(forRead ? (byte?)0 : null, forWrite ? (byte?)0 : null);
                }
            }

            private void Stop()
            {
                _stop = true;
                StopRemote();
                StopLocal();
            }

            private void StopLocal()
            {
                _syncObject.Wait();
                try
                {
                    _localConnection?.Close();
                    _localConnection = null;
                    _localStream?.Close();
                    _localStream = null;
                }
                finally
                {
                    _syncObject.Release();
                }
            }

            private void StopRemote()
            {
                _syncObject.Wait();
                try
                {
                    var remoteStreams = _remoteStreams;
                    if (remoteStreams != null)
                    {
                        // There is a potential deadlock from K8s client SDK. If remoteStreams.Dispose is invoked directly from
                        // its ConnectionStopped event handler at during connection start, it will deadlock. Move remoteStreams.Dispose
                        // off to a different thread to work around.
                        Task.Run(() => remoteStreams.Dispose()).Forget();
                    }
                    _remoteStreams = null;
                    _remote?.Dispose();
                    _remote = null;
                }
                finally
                {
                    _syncObject.Release();
                }
            }

            private void LogVerboseMessage(string message)
            {
                object[] args;
                if (_logMessagePrefixArgs != null)
                {
                    args = new object[_logMessagePrefixArgs.Length + 1];
                    Array.Copy(_logMessagePrefixArgs, args, _logMessagePrefixArgs.Length);
                    args[_logMessagePrefixArgs.Length] = message;
                }
                else
                {
                    args = new object[] { message };
                }
                _log.Verbose(_logMessagePrefixFormat, args);
            }
        }
    }
}