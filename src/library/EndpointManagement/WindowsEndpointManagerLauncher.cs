using Microsoft.BridgeToKubernetes.Common;
using System.Diagnostics;
using Microsoft.BridgeToKubernetes.Common.IO;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System.Diagnostics.Tracing;
using System.Threading;
using System.ComponentModel;
using System;
using Microsoft.BridgeToKubernetes.Common.Exceptions;

namespace Microsoft.BridgeToKubernetes.Library.EndpointManagement
{
    internal class WindowsEndpointManagerLauncher : EndpointManagerLauncherBase
    {
        public WindowsEndpointManagerLauncher(
            IEnvironmentVariables environmentVariables,
            IFileSystem fileSystem,
            ILog log,
            IOperationContext operationContext,
            IPlatform platform) : base(
                environmentVariables,
                fileSystem,
                log,
                operationContext,
                platform,
                true)
        {
        }

        public override void LaunchEndpointManager(string currentUserName, string socketFilePath, string logFileDirectory, CancellationToken cancellationToken)
        {
            EnsureLauncherExists();

            // Since EndpointManager is started as an elevated process and when a DOTNET_ROOT env variable is set,
            // on windows it is not possible to pass the DOTNET_ROOT value, so using EndpointManagerLauncher to start EndpointManager.
            // When VSCode uses BinariesV2 strategy to download clients, DOTNET_ROOT env var is set.
            var processStartInfo = string.IsNullOrEmpty(_environmentVariables.DotNetRoot)
                ? GetEndpointManagerLaunchInfoStandard(currentUserName, socketFilePath, logFileDirectory)
                : GetEndpointManagerLaunchInfoForDotnetRoot(currentUserName, socketFilePath, logFileDirectory);

            try
            {
                // Note: If the UAC prompt is declined, the process will throw a Win32Exception.
                Process.Start(processStartInfo);
            }
            catch (Exception ex) when (ex is Win32Exception)
            {
                // This catch block will be hit if the user declines the UAC prompt on Windows.
                // However, since potential other Win32Exceptions could arise, we log the exception as a warning.
                _log.ExceptionAsWarning(ex);
                throw new InvalidUsageException(_log.OperationContext, ex, Resources.LaunchProcessCancelled, EndpointManager.ProcessName);
            }
        }

        private ProcessStartInfo GetEndpointManagerLaunchInfoForDotnetRoot(string currentUserName, string socketFilePath, string logFileDirectory)
        {
            var epmLauncherPath = _fileSystem.Path.Combine(_fileSystem.Path.GetExecutingAssemblyDirectoryPath(), EndpointManager.LauncherDirectoryName, _executableName);
            var quotedArguments = $"\"{_environmentVariables.DotNetRoot.Trim('"')}\" \"{_launcherPath}\" \"{currentUserName}\" \"{socketFilePath}\" \"{logFileDirectory}\" \"{_operationContext.CorrelationId}\"";
            return GetEndpointManagerLaunchInfo(epmLauncherPath, quotedArguments);
        }

        private ProcessStartInfo GetEndpointManagerLaunchInfoStandard(string currentUserName, string socketFilePath, string logFileDirectory)
        {
            var quotedArguments = $"\"{currentUserName}\" \"{socketFilePath}\" \"{logFileDirectory}\" \"{_operationContext.CorrelationId}\"";
            return GetEndpointManagerLaunchInfo($"\"{_launcherPath}\"", quotedArguments);
        }

        private ProcessStartInfo GetEndpointManagerLaunchInfo(string launcherPath, string quotedArguments)
        {
            _log.Trace(EventLevel.Informational, $"Launcher path: {launcherPath} quotedArguments: {quotedArguments}");
            // Start launcher as admin
            ProcessStartInfo psi = new ProcessStartInfo()
            {
                FileName = launcherPath,
                Arguments = quotedArguments,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                Verb = "runas"
            };

            return psi;
        }
    }
}
