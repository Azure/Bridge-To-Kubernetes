// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// A request to execute a service
    /// </summary>
    public class ExecuteRequest
    {
        /// <summary>
        /// Ujnique identifier for this request
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>
        /// List of processes to kill to ensure a clean state
        /// </summary>
        [JsonPropertyName("processesToKill")]
        public string[] ProcessesToKill { get; set; }

        /// <summary>
        /// Service startup command
        /// </summary>
        [JsonPropertyName("serviceStartupCommand")]
        public string[] ServiceStartupCommand { get; set; }

        /// <summary>
        /// Service startup command arguments
        /// </summary>
        [JsonPropertyName("serviceStartupArguments")]
        public string[] ServiceStartupArguments { get; set; }

        /// <summary>
        /// List of commands to execute
        /// </summary>
        [JsonPropertyName("commands")]
        public CommandExecuteRequest[] Commands { get; set; }

        /// <summary>
        /// Indicates whether startup command should be run
        /// </summary>
        [JsonPropertyName("runStartupCommand")]
        public bool RunStartupCommand { get; set; }

        /// <summary>
        /// Indicates whether this is an iterative run versus standalone
        /// </summary>
        [JsonPropertyName("runIterate")]
        public bool RunIterate { get; set; }

        /// <summary>
        /// Indicates whether or not to accept input
        /// </summary>
        [JsonPropertyName("isInteractive")]
        public bool IsInteractive { get; set; }

        /// <summary>
        /// Indicates whether to live synchronize files rather than a full in-place refresh
        /// </summary>
        [JsonPropertyName("syncOnly")]
        public bool SyncOnly { get; set; }
    }
}