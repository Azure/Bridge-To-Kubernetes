using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System;
using System.Threading;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.EndpointManagement
{
    internal class LinuxEndpointManagerLauncher : EndpointManagerLauncherBase
    {
        public LinuxEndpointManagerLauncher(
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
                false)
        {
        }

        public override void LaunchEndpointManager(string currentUserName, string socketFilePath, string logFileDirectory, CancellationToken cancellationToken)
        {
            EnsureLauncherExists();

            var quotedLaunchPathAndArguments = $"\\\"{_launcherPath}\\\" \\\"{currentUserName}\\\" \\\"{socketFilePath}\\\" \\\"{logFileDirectory}\\\" \\\"{_operationContext.CorrelationId}\\\"";

            // Try to use pkexec to show a GUI prompt for the user's password so we can run EndpointManager as root.
            // If we are already running as root, then pkexec will not show a prompt.
            // When running in Codespaces the user is setup to run sudo without needing to enter password.
            var fileName = _environmentVariables.IsCodespaces ? "sudo" : "pkexec";
            var command = $"env HOME=\"{_fileSystem.HomeDirectoryPath}\" bash -c \"{quotedLaunchPathAndArguments} &> /dev/null &\"";

            _log.Info($"Launch {EndpointManager.ProcessName}: {fileName} {command}");
            var launchExitCode = LaunchExecutable(fileName, command, cancellationToken);

            if (launchExitCode == 126)
            {
                // User cancellation
                throw new InvalidUsageException(_log.OperationContext, Resources.LaunchProcessCancelled, EndpointManager.ProcessName);
            }

            // pkexec allows an authorized user to execute PROGRAM as another user. Refer - https://linux.die.net/man/1/pkexec
            // if pkexec failed then launchExitCode will not be zero, so giving user another chance to try with sudo. 
            // But this might fail for users who don't have sudo access. This is specifically for Linux.
            if (launchExitCode != 0 && !_environmentVariables.IsCodespaces)
            {
                _log.Info($"pkexec failed with exitCode {launchExitCode}, retrying with sudo");
                fileName = "sudo"; // replace pkexec with sudo
                _log.Info($"Launch {EndpointManager.ProcessName}: {fileName} {command}");
                launchExitCode = LaunchExecutable(fileName, command, cancellationToken);
            }

            if (launchExitCode != 0)
            {
                throw new InvalidOperationException($"{EndpointManager.ProcessName} exited with exit code {launchExitCode}");
            }
        }
    }
}
