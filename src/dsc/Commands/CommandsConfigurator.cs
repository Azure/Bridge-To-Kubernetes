// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    /// <summary>
    /// This class is in charge of calling Configure() on all the commands and their respective OnExecute() to complete the configuration and parameter/options reading
    /// </summary>
    internal class CommandsConfigurator
    {
        private readonly CommandLineApplication _commandLineApplication;
        private readonly Lazy<RootCommand> _rootCommand;
        private readonly Lazy<CommandLineArgumentsManager> _commandLineArgumentsManager;

        public CommandsConfigurator(CommandLineApplication commandLineApplication, Lazy<RootCommand> rootCommand, Lazy<CommandLineArgumentsManager> commandLineArgumentsManager)
        {
            this._commandLineApplication = commandLineApplication;
            this._rootCommand = rootCommand;
            this._commandLineArgumentsManager = commandLineArgumentsManager;
        }

        /// <summary>
        /// Configures all the <see cref="ITopLevelCommand"/>s so that the CLI framework can pick the right one based on the arguments
        /// </summary>
        /// <returns>0 if all commands are configured properly</returns>
        public int ConfigureCommands()
        {
            this._rootCommand.Value.Configure(this._commandLineApplication);

            // This calls the CommandLineApplication.OnExecute(). We normally use this to do a later time config, e.g. read the options and set local command fields based on that.
            // This does not run the command logic.
            return this._commandLineApplication.Execute(_commandLineArgumentsManager.Value.Arguments.ToArray());
        }
    }
}