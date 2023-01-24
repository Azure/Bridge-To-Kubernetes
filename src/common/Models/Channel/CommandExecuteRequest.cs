// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// Describes a command to execute
    /// </summary>
    public class CommandExecuteRequest
    {
        /// <summary>
        /// Command to execute
        /// </summary>
        [JsonPropertyName("command")]
        public string[] Command { get; set; }

        /// <summary>
        /// Location to execute the command
        /// </summary>
        [JsonPropertyName("workingDirectory")]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Flag to control whether to run command with a shell
        /// </summary>
        [JsonPropertyName("runWithShell")]
        public bool RunWithShell { get; set; }

        /// <summary>
        /// Flag to indicate command should not remain attached
        /// </summary>
        [JsonPropertyName("isDetach")]
        public bool IsDetach { get; set; }

        /// <summary>
        /// Command will detach when this string appears in command output
        /// </summary>
        [JsonPropertyName("detachAfter")]
        public string DetachAfter { get; set; }
    }
}