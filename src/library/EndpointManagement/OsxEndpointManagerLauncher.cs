using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System;
using System.Text;
using System.Threading;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Library.EndpointManagement
{
    internal class OsxEndpointManagerLauncher : EndpointManagerLauncherBase
    {
        public OsxEndpointManagerLauncher(
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

            var quotedLaunchPathAndArguments = $"\\\\\\\"{_launcherPath}\\\\\\\" \\\\\\\"{currentUserName}\\\\\\\" \\\\\\\"{socketFilePath}\\\\\\\" \\\\\\\"{logFileDirectory}\\\\\\\" \\\\\\\"{_operationContext.CorrelationId}\\\\\\\"";

            // We launch the EPM using AppleScript when on OSX.
            var fileName = "/usr/bin/osascript";

            // Tell the shell to redirect output so that this process exits and the EPM continues to run in the background
            // For more info: https://developer.apple.com/library/archive/technotes/tn2065/_index.html#//apple_ref/doc/uid/DTS10003093-CH1-TNTAG5-I_WANT_TO_START_A_BACKGROUND_SERVER_PROCESS__HOW_DO_I_MAKE_DO_SHELL_SCRIPT_NOT_WAIT_UNTIL_THE_COMMAND_COMPLETES_
            StringBuilder commandBuilder = new StringBuilder();
            commandBuilder.Append("-e \"do shell script ");
            commandBuilder.Append($"\\\"{quotedLaunchPathAndArguments} &> /dev/null &\\\"");
            commandBuilder.Append($" with prompt \\\"{Product.Name} wants to launch {EndpointManager.ProcessName}.\\\" with administrator privileges\"");
            var command = commandBuilder.ToString();

            _log.Info($"Launch {EndpointManager.ProcessName}: {fileName} {command}");
            var launchExitCode = LaunchExecutable(fileName, command, cancellationToken);

            if (launchExitCode == 1)
            {
                // User cancellation
                throw new InvalidUsageException(_log.OperationContext, Resources.LaunchProcessCancelled, EndpointManager.ProcessName);
            }

            if (launchExitCode != 0)
            {
                throw new InvalidOperationException($"{EndpointManager.ProcessName} exited with exit code {launchExitCode}");
            }
        }
    }
}
