// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.Commands
{
    internal interface ICommand
    {
        /// <summary>
        /// The command name
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Indicates whether logging output should be published as telemetry (e.g. sent to Application Insights)
        /// Flushing telemetry to App Insights can take more than a second, so setting this to false can improve
        /// the performance of commands that don't need to log telemetry (such as iterative up and exec commands).
        /// </summary>
        bool ShouldSendTelemetry { get; set; }

        /// <summary>
        /// CancellationToken created at the CLI application level.
        /// </summary>
        CancellationToken CancellationToken { get; set; }

        /// <summary>
        /// Configures the command.
        /// </summary>
        void Configure(CommandLineApplication commandLineApplication);

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <returns>A tuple representing the exit code and the failure reason, if any.</returns>
        Task<(ExitCode, string)> ExecuteAsync();

        /// <summary>
        /// Cleans up after the command is cancelled.
        /// </summary>
        void Cleanup();
    }
}