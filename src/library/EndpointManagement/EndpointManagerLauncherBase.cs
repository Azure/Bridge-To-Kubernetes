using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System.Threading;
using System;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Library.EndpointManagement
{
    internal abstract class EndpointManagerLauncherBase : IEndpointManagerLauncher
    {
        protected readonly IEnvironmentVariables _environmentVariables;
        protected readonly IFileSystem _fileSystem;
        protected readonly ILog _log;
        protected IOperationContext _operationContext;
        protected IPlatform _platform;
        protected readonly string _executableName;
        protected readonly string _launcherPath;

        protected EndpointManagerLauncherBase(
            IEnvironmentVariables environmentVariables,
            IFileSystem fileSystem,
            ILog log,
            IOperationContext operationContext,
            IPlatform platform,
            bool isWindows)
        {
            _environmentVariables = environmentVariables;
            _fileSystem = fileSystem;
            _log = log;
            _operationContext = operationContext;
            _platform = platform;
            _executableName = $"{EndpointManager.ProcessName}{(isWindows ? ".exe" : string.Empty)}";
            _launcherPath = _fileSystem.Path.Combine(_fileSystem.Path.GetExecutingAssemblyDirectoryPath(), EndpointManager.DirectoryName, _executableName);
        }

        public abstract void LaunchEndpointManager(string currentUserName, string socketFilePath, string logFileDirectory, CancellationToken cancellationToken);

        protected void EnsureLauncherExists()
        {
            if (!_fileSystem.FileExists(_launcherPath))
            {
                throw new InvalidOperationException($"Failed to find '{_launcherPath}'.");
            }
        }

        protected int LaunchExecutable(string executable, string command, CancellationToken cancellationToken)
        {
            var envVars = GetEnvironmentVariables();
            return _platform.Execute(
                executable: executable,
                command: command,
                logCallback: (line) => _log.Info($"Launch output: {line}"),
                envVariables: envVars,
                timeout: TimeSpan.FromSeconds(120),
                cancellationToken: cancellationToken,
                out string _);
        }

        private Dictionary<string, string> GetEnvironmentVariables()
        {
            if (!string.IsNullOrEmpty(_environmentVariables.DotNetRoot))
            {
                _log.Info("Setting the '{0}' environment variable with '{1}' while launching '{2}'.", EnvironmentVariables.Names.DotNetRoot, _environmentVariables.DotNetRoot, EndpointManager.ProcessName);
                return new Dictionary<string, string>() { { EnvironmentVariables.Names.DotNetRoot, _environmentVariables.DotNetRoot } };
            }

            return null;
        }
    }
}
