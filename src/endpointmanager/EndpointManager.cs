// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.IP;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Socket;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.EndpointManager.Logging;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// EndpointManager allocates IP addresses and updates the 'hosts' file on the local machine to reflect service route changes.
    /// </summary>
    internal class EndpointManager : AppBase
    {
        private readonly IHostsFileManager _hostsFileManager;
        private readonly Func<ISocket> _socketFactory;
        private readonly Func<string, IServiceController> _serviceControllerFactory;
        private readonly Lazy<IWindowsSystemCheckService> _windowsSystemCheckService;
        private readonly IFileSystem _fileSystem;
        private readonly IPlatform _platform;
        private readonly IAssemblyMetadataProvider _assemblyMetadataProvider;
        private readonly ILog _log;
        private readonly IOperationContext _operationContext;
        private readonly IIPManager _ipManager;

        private Timer _shutdownTimer;
        private int _shutdownTimerCount = 0;
        private string _loggedOnUserName;
        private readonly TimeSpan IdleOneMinute = TimeSpan.FromMinutes(1);
        private const int IdleShutdownTotalMinutes = 15;
        private CancellationToken _cancellationToken;
        private CancellationTokenSource _shutdownCts;
        private string _socketPath;

        public EndpointManager(
            IHostsFileManager hostsFileManager,
            Func<ISocket> socketFactory,
            Func<string, IServiceController> serviceControllerFactory,
            Lazy<IWindowsSystemCheckService> windowsSystemCheckService,
            IFileSystem fileSystem,
            IPlatform platform,
            IAssemblyMetadataProvider assemblyMetadataProvider,
            IOperationContext operationContex,
            IIPManager ipManager,
            ILog log)
        {
            _hostsFileManager = hostsFileManager;
            _socketFactory = socketFactory;
            _serviceControllerFactory = serviceControllerFactory;
            _windowsSystemCheckService = windowsSystemCheckService;
            _fileSystem = fileSystem;
            _platform = platform;
            _assemblyMetadataProvider = assemblyMetadataProvider;
            _ipManager = ipManager;
            _log = log;
            _operationContext = operationContex;
        }

        public override int Execute(string[] args, CancellationToken cancellationToken)
        {
            this._cancellationToken = cancellationToken;
            if (args.Length != 4)
            {
                throw new ArgumentException($"Received {args.Length} args. Expected 4 args: username, socketFilePath, logFileDirectory and correlationId.");
            }
            _loggedOnUserName = args[0];
            _socketPath = args[1];
            _log.Info($"Executing {nameof(EndpointManager)} with args '{string.Join(" ", args)}'");
            return (int)ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<ExitCode> ExecuteAsync()
        {
            // Try to acquire the Mutex for EndpointManager and make sure no other instances are running.
            using (var mutex = new Mutex(initiallyOwned: false, name: nameof(EndpointManager)))
            {
                try
                {
                    // TimeSpan.Zero to test the mutex's signal state and
                    // return immediately without blocking
                    bool isAnotherInstanceOpen = !mutex.WaitOne(TimeSpan.Zero);
                    if (isAnotherInstanceOpen)
                    {
                        _log.Warning("Only one instance of this app is allowed. Exiting...");
                        return ExitCode.Success;
                    }
                }
                catch (AbandonedMutexException ex)
                {
                    // Another instance of the EPM exited without releasing the mutex. Hosts file could be in a dirty state.
                    _log.ExceptionAsWarning(ex);
                    this._hostsFileManager.Clear();
                }

                this._hostsFileManager.EnsureAccess();
                try
                {
                    await this.RunAsync();
                }
                finally
                {
                    this.Cleanup();
                    try { mutex.ReleaseMutex(); } catch { }
                }
            }
            return ExitCode.Success;
        }

        private async Task RunAsync()
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.EndpointManager.AreaName,
                    Events.EndpointManager.Operations.RunAsync))
            using (_shutdownCts = new CancellationTokenSource())
            using (_cancellationToken.Register(() => _shutdownCts.Cancel()))
            {
                _log.Info($"Starting {nameof(EndpointManager)}...");
                this.StartShutdownTimer(_shutdownCts);
                using (var socket = _socketFactory.Invoke())
                {
                    if (!await WebUtilities.RetryUntilTimeWithWaitAsync(async (i) => await EstablishSocketAsync(socket),
                                                                        maxWaitTime: TimeSpan.FromSeconds(30),
                                                                        waitInterval: TimeSpan.FromSeconds(1),
                                                                        cancellationToken: _cancellationToken))
                    {
                        throw new InvalidOperationException($"{nameof(EndpointManager)} failed to establish a socket to listen on.");
                    }

                    using (_shutdownCts.Token.Register(() => socket.Close()))
                    {
                        try
                        {
                            socket.Listen();
                            _log.Info("Waiting for connections...");

                            while (!_shutdownCts.IsCancellationRequested)
                            {
                                using (var accepted = await socket.AcceptAsync()) // Blocks thread until request comes in
                                {
                                    await ProcessClientCallsAsync(accepted);
                                }
                            }
                            perfLogger.SetSucceeded();
                        }
                        catch (Exception e) when (e is SocketException socketEx && _shutdownCts.IsCancellationRequested && socketEx.SocketErrorCode == SocketError.OperationAborted)
                        {
                            // Expected when we close the socket on shutdown
                            _log.Info($"Socket closed: {e.Message}");
                            perfLogger.SetSucceeded();
                        }
                        catch (Exception e)
                        {
                            _log.Info("exception is:{e.Message}");
                            _log.Exception(e);
                        }
                    }
                }
            }
        }

        private async Task<bool> EstablishSocketAsync(ISocket socket)
        {
            try
            {
                // Clean up any previous socket instances
                var socketFileDirectory = _fileSystem.Path.GetDirectoryName(_socketPath);
                var cleanupResult = await WebUtilities.RetryUntilTimeWithWaitAsync((i) => Task.FromResult(_fileSystem.EnsureDirectoryDeleted(socketFileDirectory, recursive: true, _log)),
                        maxWaitTime: TimeSpan.FromSeconds(4),
                        waitInterval: TimeSpan.FromSeconds(1),
                        _cancellationToken);
                if (!cleanupResult)
                {
                    // This can potentially have consequences on socket connectivity, but we don't throw because there is a chance the session will still succeed.
                    // TODO(ansoedal): If this log shows up in telemetry frequently, need to take further action.
                    _log.Error("Failed to clean up the previous socket folder.");
                }

                // Prepare socketfile directory
                _fileSystem.CreateDirectory(socketFileDirectory);

                // Since it is possible to bypass socket file permissions in some cases, we lock down the directory
                // to the user only (as well as the socket file itself) in order to be extra careful.
                SetPathPermissions(socketFileDirectory, _cancellationToken);

                // Create the socket & socket file
                socket.Bind(_socketPath);

                SetPathPermissions(_socketPath, _shutdownCts.Token);
                return true;
            }
            catch (Exception ex)
            {
                _log.Exception(ex);
                return false;
            }
        }

        private async Task ProcessClientCallsAsync(ISocket socket)
        {
            _log.Info($"Client connected to Endpoint manager");
            try
            {
                // Send handshake
                await socket.SendWithEndMarkerAsync(Constants.EndpointManager.SocketHandshake);

                // Wait for request
                var request = await socket.ReadUntilEndMarkerAsync();
                var apiName = JsonHelpers.DeserializeObject<EndpointManagerRequest>(request).ApiName;
                _operationContext.CorrelationId = JsonHelpers.DeserializeObject<EndpointManagerRequest>(request).CorrelationId + LoggingConstants.CorrelationIdSeparator + LoggingUtils.NewId();

                if (!Enum.TryParse(apiName, out Constants.EndpointManager.ApiNames apiRequest))
                {
                    var apiNotRecogizedResult = new EndpointManagerResult() { IsSuccess = false, ErrorMessage = $"API '{apiName}' not recognized by {Constants.EndpointManager.ProcessName}" };
                    await SendJsonOverSocketAsync(socket, apiNotRecogizedResult);
                    return;
                }

                // Execute the request and send a response back
                EndpointManagerResult result;
                switch (apiRequest)
                {
                    case Constants.EndpointManager.ApiNames.AddHostsFileEntry:
                        var addHostsFileEntryRequest = JsonHelpers.DeserializeObject<EndpointManagerRequest<(string workloadNamespace, IEnumerable<HostsFileEntry> entries)>>(request);
                        result = this.InvokeWithExceptionHandler(() => _hostsFileManager.Add(addHostsFileEntryRequest.Argument.workloadNamespace, addHostsFileEntryRequest.Argument.entries));
                        break;

                    case Constants.EndpointManager.ApiNames.AllocateIP:
                        var allocateIpRequest = JsonHelpers.DeserializeObject<EndpointManagerRequest<IEnumerable<EndpointInfo>>>(request);
                        result = this.InvokeWithExceptionHandler<IEnumerable<EndpointInfo>>(() => _ipManager.AllocateIPs(allocateIpRequest.Argument, addRoutingRules: true, _cancellationToken));
                        break;

                    case Constants.EndpointManager.ApiNames.DisableService:
                        var disableServiceRequest = JsonHelpers.DeserializeObject<EndpointManagerRequest<IEnumerable<ServicePortMapping>>>(request);
                        var servicePortMappings = disableServiceRequest.Argument;
                        result = this.InvokeWithExceptionHandler(() => servicePortMappings.ExecuteForEach(mapping => DisableService(mapping)));
                        break;

                    case Constants.EndpointManager.ApiNames.FreeIP:
                        var freeIPRequest = JsonHelpers.DeserializeObject<EndpointManagerRequest<IPAddress[]>>(request);
                        result = this.InvokeWithExceptionHandler(() => _ipManager.FreeIPs(freeIPRequest.Argument, _hostsFileManager, removeRoutingRules: true, _cancellationToken));
                        break;

                    case Constants.EndpointManager.ApiNames.KillProcess:
                        var killProcessRequest = JsonHelpers.DeserializeObject<EndpointManagerRequest<IEnumerable<ProcessPortMapping>>>(request);
                        var processPortMapping = killProcessRequest.Argument;
                        result = this.InvokeWithExceptionHandler(() => processPortMapping.ExecuteForEach(mapping => KillProcess(mapping)));
                        break;

                    case Constants.EndpointManager.ApiNames.Ping:
                        RegisterClientPing();
                        result = new EndpointManagerResult { IsSuccess = true };
                        break;

                    case Constants.EndpointManager.ApiNames.SystemCheck:
                        result = this.InvokeWithExceptionHandler<EndpointManagerSystemCheckMessage>(() => SystemCheck());
                        break;

                    case Constants.EndpointManager.ApiNames.Stop:
                        result = this.InvokeWithExceptionHandler(() => _shutdownCts.Cancel());
                        break;

                    case Constants.EndpointManager.ApiNames.Version:
                        RegisterClientPing();
                        result = new EndpointManagerResult<string>
                        {
                            IsSuccess = true,
                            Value = this._assemblyMetadataProvider.AssemblyVersion
                        };
                        break;

                    default:
                        throw new NotSupportedException($"API '{apiRequest}' not yet supported by {Constants.EndpointManager.ProcessName}");
                }
                await SendJsonOverSocketAsync(socket, result);
            }
            catch (Exception e)
            {
                _log.Exception(e);
                if (socket != null && socket.Connected)
                {
                    var result = new EndpointManagerResult() { IsSuccess = false, ErrorMessage = e.Message };
                    await SendJsonOverSocketAsync(socket, result);
                }
            }
        }

        #region free ports

        private void KillProcess(ProcessPortMapping portMapping)
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.EndpointManager.AreaName,
                    Events.EndpointManager.Operations.KillProcess))
            {
                _log.Verbose($"Killing process using port '{portMapping.PortNumber}'");
                try
                {
                    _platform.KillProcess(portMapping.ProcessId);
                    perfLogger.SetSucceeded();
                    _log.Verbose($"Port {portMapping.PortNumber} freed.");
                }
                catch (Exception e)
                {
                    // The process has already exited
                    _log.ExceptionAsWarning(e);
                }
            }
        }

        private void DisableService(ServicePortMapping portMapping)
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.EndpointManager.AreaName,
                    Events.EndpointManager.Operations.DisableService))
            {
                _log.Verbose($"Disabling service '{portMapping.ServiceName}' using port '{portMapping.PortNumber}'");
                if (!Constants.EndpointManager.NonCriticalWindowsPortListeningServices.ContainsKey(portMapping.ServiceName))
                {
                    throw new UserVisibleException(_log.OperationContext, Resources.CannotRecognizeServiceFormat, portMapping.ServiceName, portMapping.PortNumber);
                }

                try
                {
                    using (var service = _serviceControllerFactory.Invoke(portMapping.ServiceName))
                    {
                        service.Stop();
                        service.WaitForStatus(ServiceControllerStatus.Stopped);
                    }

                    if (StringComparer.OrdinalIgnoreCase.Equals(Constants.EndpointManager.KnownProcesses.BranchCacheDisplayName, portMapping.ServiceName))
                    {
                        // If the service is BranchCache, set the startup type to disabled
                        var result = _platform.Execute(
                                                executable: "sc.exe",
                                                command: $"config {Constants.EndpointManager.KnownProcesses.BranchCacheServiceName} start=disabled",
                                                logCallback: (line) => _log.Verbose($"sc.exe: {line}"),
                                                envVariables: null,
                                                timeout: TimeSpan.FromSeconds(10),
                                                cancellationToken: _cancellationToken,
                                                out string output);

                        if (result != 0)
                        {
                            _log.Warning($"Failed to set {Constants.EndpointManager.KnownProcesses.BranchCacheDisplayName} startup type to disabled. Exit code: {result}, Output: {output}");
                        }
                    }

                    perfLogger.SetSucceeded();
                    _log.Verbose($"Port {portMapping.PortNumber} freed.");
                }
                catch (Exception e) when (e is InvalidOperationException)
                {
                    // The service was not found
                    _log.ExceptionAsWarning(e);
                }
            }
        }

        private EndpointManagerSystemCheckMessage SystemCheck()
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.EndpointManager.AreaName,
                    Events.EndpointManager.Operations.SystemCheck))
            {
                if (_platform.IsWindows)
                {
                    _log.Info("Checking Windows system for services and processes known to use needed ports...");
                    var checkResult = _windowsSystemCheckService.Value.RunCheck();
                    perfLogger.SetSucceeded();
                    _log.Info($"Windows system check complete.");
                    return checkResult;
                }
                else
                {
                    _log.Info("Detected OS other than Windows. Skipping performing Windows system check.");
                    perfLogger.SetSucceeded();
                    return new EndpointManagerSystemCheckMessage()
                    {
                        ServiceMessages = new SystemServiceCheckMessage[] { },
                        PortBinding = new Dictionary<int, string>()
                    };
                }
            }
        }

        #endregion free ports

        #region socket communication

        private void SetPathPermissions(string path, CancellationToken cancellationToken)
        {
            _log.Info($"Setting permissions on '{new PII(path)}'");
            _fileSystem.SetAccessPermissions(path, FileSystemRights.Modify, logCallback: (line) => _log.Verbose(line), cancellationToken, _loggedOnUserName);
        }

        private async Task SendJsonOverSocketAsync(ISocket server, EndpointManagerResult result)
        {
            try
            {
                var serializedResult = JsonHelpers.SerializeObject(result);
                _log.Info($"Sending response: '{serializedResult}'");
                var numBytes = await server.SendWithEndMarkerAsync(serializedResult);
            }
            catch (Exception e)
            {
                _log.Exception(e);
            }
        }

        private EndpointManagerResult InvokeWithExceptionHandler(Action handler)
        {
            EndpointManagerResult result = new EndpointManagerResult();
            try
            {
                handler();
                result.ErrorMessage = result.ErrorType = string.Empty;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _log.Exception(ex);

                // We want to show UserVisible exceptions to the user on the CLI side, as well as some other exceptions identified from telemetry
                // that are helpful to bubble up (e.g. the hosts file being used by another process)
                if (ex is IUserVisibleExceptionReporter
                    || (ex.Message.Contains("The process cannot access the file"))
                    || (ex.Message.Contains("This program is blocked by group policy")))
                {
                    result.ErrorType = Constants.EndpointManager.Errors.UserVisible.ToString();
                }
                else
                {
                    result.ErrorType = Constants.EndpointManager.Errors.InvalidOperation.ToString();
                }

                result.ErrorMessage = ex.Message;
                result.IsSuccess = false;
            }
            return result;
        }

        private EndpointManagerResult<T> InvokeWithExceptionHandler<T>(Func<T> handler)
        {
            EndpointManagerResult<T> result = new EndpointManagerResult<T>();
            try
            {
                result.Value = handler();
                result.ErrorMessage = result.ErrorType = string.Empty;
                result.IsSuccess = true;
            }
            catch (Exception ex)
            {
                _log.Exception(ex);

                if (ex is IUserVisibleExceptionReporter)
                {
                    _log.Info("Returning user visible error with the response");
                    result.ErrorType = Constants.EndpointManager.Errors.UserVisible.ToString();
                }
                else
                {
                    result.ErrorType = Constants.EndpointManager.Errors.InvalidOperation.ToString();
                }

                result.ErrorMessage = ex.Message;
                result.IsSuccess = false;
            }
            return result;
        }

        #endregion socket communication

        #region timer

        private void RegisterClientPing()
        {
            _log.Info("Registering client ping");
            this.RefreshShutdownTimer();
        }

        private void StartShutdownTimer(CancellationTokenSource shutdownCts)
        {
            _log.Verbose("Starting internal timer");
            _shutdownTimer?.Dispose();
            _shutdownTimer = new Timer((_) =>
            {
                var i = Interlocked.Increment(ref _shutdownTimerCount);
                if (i >= IdleShutdownTotalMinutes)
                {
                    // Clean up and stop the EndpointManager
                    var terminate = $"Idle timeout reached. {nameof(EndpointManager)} shutting down.";
                    _log.Info(terminate);
                    try
                    {
                        shutdownCts.Cancel();
                    }
                    catch (ObjectDisposedException)
                    {
                        _log.Warning($"{nameof(shutdownCts)} already disposed");
                    }
                }
            }, null, IdleOneMinute, IdleOneMinute);
        }

        /// <summary>
        /// Restart the shutdown timer when the client pings. Shutdown timer employs 2 counters to guard against scenarios such as system
        /// sleep. The first counter triggers every minute, and shutdown will happen only if no client ping for 15 consecutive minutes.
        /// </summary>
        private void RefreshShutdownTimer()
        {
            _log.Info("Refreshing internal timer");
            Interlocked.Exchange(ref this._shutdownTimerCount, 0);  // Reset _shutdownTimerCount value.
            _shutdownTimer?.Change(IdleOneMinute, IdleOneMinute);
        }

        #endregion timer

        private void Cleanup()
        {
            using (var perfLogger =
                _log.StartPerformanceLogger(
                    Events.EndpointManager.AreaName,
                    Events.EndpointManager.Operations.Cleanup))
            {
                _log.Verbose("Cleaning up...");
                _shutdownTimer?.Dispose();
                _shutdownTimer = null;
                _hostsFileManager.Clear();

                _log.Info("Removing IP allocation and routing rules...");
                _ipManager.Dispose();

                _log.Verbose("Removing previous socket instance...");
                _fileSystem.EnsureDirectoryDeleted(_fileSystem.Path.GetDirectoryName(_socketPath), recursive: true, _log);

                _log.Info("Cleanup complete.");
                perfLogger.SetSucceeded();
                _log.Flush(TimeSpan.FromMilliseconds(1500));
            }
        }
    }
}