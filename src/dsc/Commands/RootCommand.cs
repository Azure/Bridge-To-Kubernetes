// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.Extensions.CommandLineUtils;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    internal class RootCommand : ICommand
    {
        private CommandLineApplication _commandLineApp;
        private IEnumerable<ITopLevelCommand> _topLevelCommands;
        private IAssemblyMetadataProvider _assemblyMetadataProvider;

        /// <summary>
        /// Name doesn't apply to Root command
        /// </summary>
        public string Name => null;

        public virtual bool ShouldSendTelemetry { get; set; } = true;

        public CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="topLevelCommands">All registered ITopLevelCommands</param>
        public RootCommand(IEnumerable<ITopLevelCommand> topLevelCommands, IAssemblyMetadataProvider assemblyMetadataProvider)
        {
            this._topLevelCommands = topLevelCommands;
            this._assemblyMetadataProvider = assemblyMetadataProvider;
        }

        public void Configure(CommandLineApplication app)
        {
            this._commandLineApp = app;

            foreach (var command in this._topLevelCommands)
            {
                this._commandLineApp.Command(command.Name, a => command.Configure(a));
            }

            var versionDisplayString = $"{_assemblyMetadataProvider.AssemblyVersion}";

            this._commandLineApp.VersionOption(CommandConstants.Options.Version, versionDisplayString, versionDisplayString);
            this._commandLineApp.HelpOption($"{CommandConstants.Options.Help.Short}|{CommandConstants.Options.Help.Long}");

            this._commandLineApp.OnExecute(async () =>
            {
                ExitCode exitCode = ExitCode.Success;
                string failureReason = string.Empty;
                (exitCode, failureReason) = await this.ExecuteAsync();
                return (int)exitCode;
            });
        }

        public Task<(ExitCode, string)> ExecuteAsync()
        {
            this._commandLineApp.ShowHelp();
            return Task.FromResult((ExitCode.Success, string.Empty));
        }

        public void Cleanup()
        {
        }
    }
}