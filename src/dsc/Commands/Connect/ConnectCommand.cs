// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.IO.Input;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Exe.Output.Models;
using Microsoft.BridgeToKubernetes.Exe.Remoting;
using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe.Commands.Connect
{
    internal class ConnectCommand : TargetConnectCommandBase, ITopLevelCommand
    {
        private readonly Lazy<IPlatform> _platform;
        private readonly Lazy<IFileSystem> _fileSystem;
        private readonly Lazy<IConsoleLauncher> _consoleLauncher;
        private readonly Func<IWebHostBuilder> _webHostBuilderFactory;

        private string _routingHeaderValue = string.Empty;
        private bool _useKubernetesServiceEnvironmentVariables = false;
        private IEnumerable<IElevationRequest> _elevationRequests;
        private int[] _localPorts = new int[] { };
        private string _updateScript = string.Empty;
        private string _envScriptPath = string.Empty;
        private bool _cleanupPerformed = false;
        private string _envJsonPath = string.Empty;
        private bool _runContainerized = false;
        private List<Process> _waitProcesses = new List<Process>();
        private CancellationTokenSource _connectionCancellationSource = new CancellationTokenSource();
        private CancellationTokenSource _executeCancellationSource;
        private CancellationTokenRegistration _connectionCancellationSourceRegistration = default(CancellationTokenRegistration);
        private int _controlPort = 0;
        private bool _yesFlag = false;
        private IEnumerable<string> _routingManagerFeatureFlags = null;

        public override string Name => CommandConstants.Connect;

        public ConnectCommand(
            CommandLineArgumentsManager commandLineArgumentsManager,
            IManagementClientFactory clientFactory,
            ILog log,
            IOperationContext operationContext,
            IConsoleInput consoleInput,
            IConsoleOutput consoleOutput,
            IProgress<ProgressUpdate> progress,
            ICliCommandOptionFactory cliCommandOptionFactory,
            ISdkErrorHandling sdkErrorHandling,
            Lazy<IPlatform> platform,
            Lazy<IFileSystem> fileSystem,
            Lazy<IConsoleLauncher> consoleLauncher,
            Func<IWebHostBuilder> webHostBuilderFactory)
                : base(
                  commandLineArgumentsManager,
                  clientFactory,
                  log,
                  operationContext,
                  consoleInput,
                  consoleOutput,
                  progress,
                  cliCommandOptionFactory,
                  sdkErrorHandling)
        {
            _platform = platform;
            _fileSystem = fileSystem;
            _consoleLauncher = consoleLauncher;
            _webHostBuilderFactory = webHostBuilderFactory;
        }

        public override void Configure(CommandLineApplication app)
        {
            this._command = app;
            this._command.ShowInHelpText = true;
            this._command.AllowArgumentSeparator = true;
            base.ConfigureHelp(
                description: "Redirect traffic from a service, deployment or pod running in your cluster to your local machine.",
                extendedHelpText: @"
Additional Arguments
  Any arguments passed following the argument separator ""--"" will be interpreted as a command to execute in the Bridge
  environment. This can be used to connect to Kubernetes and start a service in one command line invocation.
  Example: 
  dsc connect --service my-service --local-port 8000 --routing me -y -- dotnet watch --project MyService.API
");

            base.Configure(app);

            var localPortOption = _cliCommandOptionFactory.CreateConnectLocalPortOption();
            var updateScriptOption = _cliCommandOptionFactory.CreateConnectUpdateScriptOption();
            var envOption = _cliCommandOptionFactory.CreateConnectEnvOption();
            var waitPpidOption = _cliCommandOptionFactory.CreateParentProcessIdOption();
            var controlPortOption = _cliCommandOptionFactory.CreateControlPortOption();
            var elevationRequestsOption = _cliCommandOptionFactory.CreateConnectElevationRequestsOptions();
            var routingOption = _cliCommandOptionFactory.CreateConnectRoutingHeaderOption();
            var useKubernetesServiceEnvironmentVariablesOption = _cliCommandOptionFactory.CreateUseKubernetesServiceEnvironmentVariablesOption();
            var runContainerizedOption = _cliCommandOptionFactory.CreateRunContainerizedOption();
            var yesOption = _cliCommandOptionFactory.CreateYesOption();
            var routingManagerFeatureFlagsOption = _cliCommandOptionFactory.CreateRoutingManagerFeatureFlagOption();

            this._command.Options.Add(localPortOption);
            this._command.Options.Add(updateScriptOption);
            this._command.Options.Add(envOption);
            this._command.Options.Add(waitPpidOption);
            this._command.Options.Add(controlPortOption);
            this._command.Options.Add(elevationRequestsOption);
            this._command.Options.Add(routingOption);
            this._command.Options.Add(useKubernetesServiceEnvironmentVariablesOption);
            this._command.Options.Add(runContainerizedOption);
            this._command.Options.Add(yesOption);
            this._command.Options.Add(routingManagerFeatureFlagsOption);

            this._command.OnExecute(() =>
            {
                try
                {
                    this.ParseTargetOptions();
                }
                catch (Exception ex)
                {
                    _out.Error(ex.Message);
                    this._command.ShowHelp();
                    return 1;
                }
                if (localPortOption.HasValue())
                {
                    try
                    {
                        _localPorts = localPortOption.Values.Select(s => int.Parse(s)).ToArray();
                        if (_localPorts.Any(s => s < 0 || s >= 65536))
                        {
                            throw new InvalidUsageException(_operationContext, string.Format(Resources.Error_InvalidPort, localPortOption.Template));
                        }
                    }
                    catch (FormatException ex)
                    {
                        _out.Error(string.Format(Resources.Error_IncorrectOption, CommandConstants.Options.ConnectLocalPort.Option, ex.Message));
                        this._command.ShowHelp();
                        return 1;
                    }
                }
                if (updateScriptOption.HasValue())
                {
                    _updateScript = updateScriptOption.Value();
                }
                if (envOption.HasValue())
                {
                    _envJsonPath = envOption.Value();
                }
                if (waitPpidOption.HasValue())
                {
                    if (int.TryParse(waitPpidOption.Value(), out var pid) && pid > 0)
                    {
                        try
                        {
                            _waitProcesses.Add(Process.GetProcessById(pid));
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidUsageException(_operationContext, string.Format(Resources.Error_FailedToGetProcess, pid, ex.Message));
                        }
                    }
                }
                if (controlPortOption.HasValue())
                {
                    if (!int.TryParse(controlPortOption.Value(), out _controlPort) || _controlPort <= 0 || _controlPort >= 65536)
                    {
                        throw new InvalidUsageException(_operationContext, string.Format(Resources.Error_InvalidPort, controlPortOption.Template));
                    }
                }
                if (elevationRequestsOption.HasValue())
                {
                    try
                    {
                        var jsonElevationRequests = elevationRequestsOption.Value();
                        var elevationRequestsData = JsonHelpers.DeserializeObjectCaseInsensitive<IEnumerable<ElevationRequestData>>(jsonElevationRequests);
                        this._elevationRequests = elevationRequestsData.Select(erd => erd.ConvertToElevationRequest()).ToList();
                    }
                    catch (Exception)
                    {
                        throw new InvalidUsageException(_operationContext, string.Format(Resources.Error_InvalidElevationRequestsValue, elevationRequestsOption.Template));
                    }
                }

                this._operationContext.LoggingProperties[LoggingConstants.Property.IsRoutingEnabled] = routingOption.HasValue();
                if (routingOption.HasValue())
                {
                    try
                    {
                        KubernetesUtilities.IsValidRoutingValue(routingOption.Value());
                        this._routingHeaderValue = routingOption.Value();
                    }
                    catch (Exception e)
                    {
                        throw new InvalidUsageException(_operationContext, e.Message);
                    }
                }
                if (routingManagerFeatureFlagsOption.HasValue())
                {
                    _routingManagerFeatureFlags = routingManagerFeatureFlagsOption.Values;
                }

                this._useKubernetesServiceEnvironmentVariables = useKubernetesServiceEnvironmentVariablesOption.HasValue();
                this._yesFlag = yesOption.HasValue();

                this._runContainerized = runContainerizedOption.HasValue();

                this.SetCommand();
                return 0;
            });
        }

        public override async Task<(ExitCode, string)> ExecuteAsync()
        {
            IRoutingManagementClient routingManagementClient = null;
            IConnectManagementClient connectManagementClient = null;
            IKubernetesManagementClient kubernetesManagementClient = null;
            KubeConfigDetails kubeConfigDetails = null;

            var failureReason = string.Empty;
            try
            {
                this.OnExecute();
                using (IKubeConfigManagementClient kubeConfigClient = _clientFactory.CreateKubeConfigClient(_targetKubeConfigContext))
                {
                    kubeConfigDetails = kubeConfigClient.GetKubeConfigDetails();
                    kubernetesManagementClient = _clientFactory.CreateKubernetesManagementClient(kubeConfigDetails);
                }

                if (!await kubernetesManagementClient.CheckCredentialsAsync(_targetNamespace, this.CancellationToken))
                {
                    throw new InvalidOperationException(Resources.FailedToPerformActionLoginNeeded);
                }

                // If it's not passed, read the namespace from the kubeconfig
                if (string.IsNullOrWhiteSpace(_targetNamespace))
                {
                    this._targetNamespace = kubeConfigDetails.NamespaceName;
                }

                if (_controlPort > 0)
                {
                    using (this.CancellationToken.Register(() => _connectionCancellationSource?.Cancel()))
                    {
                        RemotingHelper.StartRemotingServer(_webHostBuilderFactory, _controlPort, _log, _connectionCancellationSource);
                    }
                }

                RemoteContainerConnectionDetails remoteContainerConnectionDetails = this.ResolveContainerConnectionDetails(_routingHeaderValue, _routingManagerFeatureFlags);

                connectManagementClient = _clientFactory.CreateConnectManagementClient(remoteContainerConnectionDetails, kubeConfigDetails, _useKubernetesServiceEnvironmentVariables, _runContainerized);

                if (!string.IsNullOrEmpty(_routingHeaderValue))
                {
                    routingManagementClient = _clientFactory.CreateRoutingManagementClient(remoteContainerConnectionDetails.NamespaceName, kubeConfigDetails);
                    var validationErrorResponse = await routingManagementClient.GetValidationErrorsAsync(_routingHeaderValue, _connectionCancellationSource.Token);
                    if (validationErrorResponse != null && !string.IsNullOrEmpty(validationErrorResponse.Value))
                    {
                        _out.Error(validationErrorResponse.Value);
                        return (ExitCode.Fail, validationErrorResponse.Value);
                    }
                    var workloadInfo = await connectManagementClient.GetWorkloadInfo();
                    if (workloadInfo.EnvironmentVariables.ContainsKey("DAPR_HTTP_PORT") &&
                        workloadInfo.EnvironmentVariables.ContainsKey("DAPR_GRPC_PORT"))
                    {
                        _out.Error(Resources.IsolationIsNotAvailableWithDaprError);
                        return (ExitCode.Fail, Resources.IsolationIsNotAvailableWithDaprError);
                    }
                }

                ExitCode exitCode = ExitCode.Success;
                if (_runContainerized)
                {
                    return await this.ExecuteInnerContainerizedAsync(connectManagementClient, kubeConfigDetails, this._connectionCancellationSource.Token, routingManagementClient);
                }

                if (this._elevationRequests == null && !_useKubernetesServiceEnvironmentVariables)
                {
                    this._elevationRequests = await connectManagementClient.GetElevationRequestsAsync(CancellationToken);
                    if (this._elevationRequests != null && this._elevationRequests.Any() && !_yesFlag)
                    {
                        // The CLI is being called directly by the user. We output the elevation requests and wait for confirmation before proceeding
                        var sb = new StringBuilder();
                        sb.AppendLine(string.Format(Resources.AdminPermissionsRequiredFormat, Product.NameAbbreviation));
                        foreach (var request in this._elevationRequests)
                        {
                            sb.AppendLine(request.ConvertToReadableString());
                        }
                        sb.AppendLine();
                        sb.AppendLine(Resources.AdminPermissionsDisclaimer);
                        _out.Info(sb.ToString(), newLine: false);

                        if (!ConfirmContinue(defaultConfirmation: Confirmation.No, confirmationMessage: Resources.AdminPermissionsPrompt))
                        {
                            return (ExitCode.Cancel, string.Empty);
                        }
                    }
                }

                _envScriptPath = string.IsNullOrEmpty(_updateScript) ? this.CreateEnvScriptPath() : _updateScript;

                if (!_useKubernetesServiceEnvironmentVariables && (this._elevationRequests != null && this._elevationRequests.Any()))
                {
                    // Invoke the endpoint manager.
                    await connectManagementClient.StartEndpointManagerAsync(CancellationToken);
                }

                bool _relaunch = false;
                do
                {
                    this.RefreshExecuteCancellationSource();
                    _relaunch = false;
                    (exitCode, failureReason) = await this.ExecuteInnerAsync(connectManagementClient, () =>
                    {
                        Task.Run(async () =>
                        {
                            await connectManagementClient.WaitRemoteAgentChangeAsync(this._connectionCancellationSource.Token);

                            _relaunch = true;
                            if (!_executeCancellationSource.IsCancellationRequested)
                            {
                                _executeCancellationSource.Cancel();
                            }
                        }).Forget();
                    }, _executeCancellationSource.Token, routingManagementClient);
                    if (_relaunch && !this._connectionCancellationSource.IsCancellationRequested)
                    {
                        _out.Info(Resources.WorkloadChangeDetected);
                    }
                } while (_relaunch);
                return (exitCode, failureReason);
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ServiceRunPortForward, out failureReason))
            {
                // Message has been logged. Continue.
            }
            finally
            {
                _executeCancellationSource?.Dispose();
                _connectionCancellationSourceRegistration.Dispose();
                _connectionCancellationSource?.Dispose();

                // Dispose management clients
                routingManagementClient?.Dispose();
                connectManagementClient?.Dispose();
                kubernetesManagementClient?.Dispose();
            }

            return (ExitCode.Fail, failureReason);
        }

        private void RefreshExecuteCancellationSource()
        {
            _executeCancellationSource = new CancellationTokenSource();
            _connectionCancellationSourceRegistration.Dispose();
            _connectionCancellationSourceRegistration = this._connectionCancellationSource.Token.Register(() =>
           {
               if (!_executeCancellationSource.IsCancellationRequested)
               {
                   _executeCancellationSource.Cancel();

                   int c = 0;
                   while (!_cleanupPerformed && c < 50)
                   {
                       c++;
                       Thread.Sleep(100);
                   }
               }
           });
        }

        private async Task<(ExitCode, string)> ExecuteInnerAsync(IConnectManagementClient connectManagementClient, Action workloadStartedHandler, CancellationToken cancellationToken, IRoutingManagementClient routingManagementClient = null)
        {
            try
            {
                if (!string.IsNullOrEmpty(_routingHeaderValue))
                {
                    await routingManagementClient.DeployRoutingManagerAsync(cancellationToken);
                }

                var remoteAgentInfo = await connectManagementClient.StartRemoteAgentAsync(cancellationToken);
                var remoteAgentLocalPort = await connectManagementClient.ConnectToRemoteAgentAsync(remoteAgentInfo, cancellationToken);

                if (!string.IsNullOrEmpty(_routingHeaderValue))
                {
                    // If routing is enabled, wait until the routing manager is connected to say the connection has been established successfully.
                    var routingStatus = await routingManagementClient.GetStatusAsync(remoteAgentInfo.PodName, cancellationToken);
                    if (routingStatus.Value.IsConnected == null)
                    {
                        _log.Error(routingStatus.Value.ErrorMessage);
                        throw new UserVisibleException(_operationContext, Resources.FailedToGetRoutingManagerDeploymentStatusFormat, string.Format(CommonResources.CorrelationIdErrorMessageFormat, _operationContext.CorrelationId));
                    }
                    else if (routingStatus.Value.IsConnected == false)
                    {
                        throw new UserVisibleException(_operationContext, Resources.FailedToGetRoutingManagerDeploymentStatusRanOutOfTimeFormat, routingStatus.Value.ErrorMessage + string.Format(CommonResources.CorrelationIdErrorMessageFormat, _operationContext.CorrelationId));
                    }
                }
                this.ReportProgress(EventLevel.Informational, Resources.Progress_ConnectionEstablished);

                // Map ports for reverse port forwarding, and ports and IPs for service port forwardings
                await connectManagementClient.AddLocalMappingsAsync(_localPorts, _elevationRequests, cancellationToken);
                var workloadInfo = await connectManagementClient.GetWorkloadInfo();
                await connectManagementClient.StartServicePortForwardingsAsync(remoteAgentLocalPort, workloadInfo.ReachableEndpoints, workloadInfo.ReversePortForwardInfo, cancellationToken);
                var envVars = await connectManagementClient.GetLocalEnvironment(_localPorts, cancellationToken);
                if (!string.IsNullOrEmpty(_envJsonPath))
                {
                    _fileSystem.Value.WriteAllTextToFile(_envJsonPath, JsonHelpers.SerializeObjectIndented(envVars));
                }
                this.ReportProgress(EventLevel.LogAlways, $"##################### {Resources.Progress_EnvironmentStarted} #############################################################");
                if (string.IsNullOrEmpty(_updateScript))
                {
                    var consoleProcess = _consoleLauncher.Value.LaunchTerminalWithEnv(envVars, _envScriptPath, launchCommand: _commandLineArgumentsManager.CommandExecuteArguments);
                    _waitProcesses.Add(consoleProcess);
                }
                else
                {
                    _consoleLauncher.Value.LaunchTerminalWithEnv(envVars, _envScriptPath, performLaunch: false);
                }
                this.ReportProgress(EventLevel.LogAlways, string.Format(Resources.Progress_RunScriptToConnect, _envScriptPath));

                workloadStartedHandler();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_waitProcesses.Any() && !_waitProcesses.Where(p => !p.HasExited).Any())
                    {
                        // all waitProcesses are terminated.
                        break;
                    }
                    cancellationToken.WaitHandle.WaitOne(1000);
                }
                return (ExitCode.Success, string.Empty);
            }
            catch (OperationCanceledException)
            {
                // Expected for StopConnect scenario. Message has been logged. Continue.
                return (ExitCode.Success, string.Empty);
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ServiceRunPortForward, out string failureReason))
            {
                // Message has been logged. Continue.
                return (ExitCode.Fail, failureReason);
            }
            catch (Exception ex)
            {
                if (!(ex is TaskCanceledException) && !(ex is OperationCanceledException))
                {
                    _log.Error($"ServiceConnectCommand.ExecuteInnerAsync caught exception {ex.ToString()}");
                    _out.Error(Resources.Error_ConnectOperationFailed);
                }
                throw;
            }
            finally
            {
                // We attempt to restore remote workload and stop all port forward connections regardless of success/failure of the operation.
                // In case of a failure we attempt to restore as much as possible and some of the restore/stop workflows might be essentially no-op,
                // example if remote agent connection fails, there is no local workload connection to close because the port forwarding connections
                // have not been made and no local IPs have been allocated for remote services.
                this.ReportProgress(EventLevel.LogAlways, Resources.Progress_StoppingWorkload);
                await Task.WhenAll(connectManagementClient.RestoreOriginalRemoteContainerAsync(CancellationToken.None),
                                   connectManagementClient.StopLocalConnectionAsync(CancellationToken.None));
                _cleanupPerformed = true;
            }
        }

        private async Task<(ExitCode, string)> ExecuteInnerContainerizedAsync(IConnectManagementClient connectManagementClient, KubeConfigDetails kubeConfigDetails, CancellationToken cancellationToken, IRoutingManagementClient routingManagementClient = null)
        {
            string localAgentContainerName = string.Empty;
            try
            {
                // TODO (lolodi): To speed things up we should deploy routing manager and remote agent at the same time, instead of awaiting on the deployment of one before deploying the other.
                // This should also be done in the normal ExecuteInnerAsync
                if (!string.IsNullOrEmpty(_routingHeaderValue))
                {
                    await routingManagementClient.DeployRoutingManagerAsync(cancellationToken);
                }

                var remoteAgentInfo = await connectManagementClient.StartRemoteAgentAsync(cancellationToken);

                if (!string.IsNullOrEmpty(_routingHeaderValue))
                {
                    // If routing is enabled, wait until the routing manager is connected to say the connection has been established successfully.
                    var routingStatus = await routingManagementClient.GetStatusAsync(remoteAgentInfo.PodName, cancellationToken);
                    if (routingStatus.Value.IsConnected == null)
                    {
                        _log.Error(routingStatus.Value.ErrorMessage);
                        throw new UserVisibleException(_operationContext, Resources.FailedToGetRoutingManagerDeploymentStatusFormat, string.Format(CommonResources.CorrelationIdErrorMessageFormat, _operationContext.CorrelationId));
                    }
                    else if (routingStatus.Value.IsConnected == false)
                    {
                        throw new UserVisibleException(_operationContext, Resources.FailedToGetRoutingManagerDeploymentStatusRanOutOfTimeFormat, routingStatus.Value.ErrorMessage + string.Format(CommonResources.CorrelationIdErrorMessageFormat, _operationContext.CorrelationId));
                    }
                }

                // this.ReportProgress(EventLevel.Informational, Resources.Progress_ConnectionEstablished); this should be "remote agent deployed"
                localAgentContainerName = await connectManagementClient.StartLocalAgentAsync(_localPorts, kubeConfigDetails, remoteAgentInfo, cancellationToken);
                // TODO: report progress local agent started

                var envVars = await connectManagementClient.GetLocalEnvironment(_localPorts, cancellationToken);

                // This is the env file that should be used
                if (!string.IsNullOrEmpty(_envJsonPath))
                {
                    _fileSystem.Value.WriteAllTextToFile(_envJsonPath, JsonHelpers.SerializeObjectIndented(envVars));
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (_waitProcesses.Any() && !_waitProcesses.Where(p => !p.HasExited).Any())
                    {
                        // all waitProcesses are terminated.
                        break;
                    }
                    cancellationToken.WaitHandle.WaitOne(1000);
                }
                return (ExitCode.Success, string.Empty);
            }
            catch (OperationCanceledException)
            {
                // Expected for StopConnect scenario. Message has been logged. Continue.
                return (ExitCode.Success, string.Empty);
            }
            catch (Exception e) when (base._sdkErrorHandling.TryHandleKnownException(e, CliConstants.Dependency.ServiceRunPortForward, out string failureReason))
            {
                // Message has been logged. Continue.
                return (ExitCode.Fail, failureReason);
            }
            catch (Exception ex)
            {
                if (!(ex is TaskCanceledException) && !(ex is OperationCanceledException))
                {
                    _log.Error($"ServiceConnectCommand.ExecuteInnerContainerizedAsync caught exception {ex.ToString()}");
                    _out.Error(Resources.Error_ConnectOperationFailed);
                }
                throw;
            }
            finally
            {
                // We attempt to restore remote workload and stop all port forward connections regardless of success/failure of the operation.
                // In case of a failure we attempt to restore as much as possible and some of the restore/stop workflows might be essentially no-op,
                // example if remote agent connection fails, there is no local workload connection to close because the port forwarding connections
                // have not been made and no local IPs have been allocated for remote services.
                this.ReportProgress(EventLevel.LogAlways, Resources.Progress_StoppingWorkload);
                await Task.WhenAll(connectManagementClient.RestoreOriginalRemoteContainerAsync(CancellationToken.None),
                                   connectManagementClient.StopLocalAgentAsync(localAgentContainerName, CancellationToken.None));
                _cleanupPerformed = true;
            }
        }

        private string CreateEnvScriptPath()
        {
            var scriptFileExtension = _platform.Value.IsWindows ? ".cmd" : ".sh";
            var scriptFileName = $"{Guid.NewGuid().ToString("N").Substring(0, 8)}{scriptFileExtension}";

            return _fileSystem.Value.Path.GetTempFilePath(scriptFileName);
        }

        private void ReportProgress(EventLevel eventLevel, string message)
            => _progress.Report(new ProgressUpdate(0, ProgressStatus.LocalConnect, new ProgressMessage(eventLevel, message)));
    }
}