// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Newtonsoft.Json;

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
        [JsonProperty("command")]
        public string[] Command { get; set; }

        /// <summary>
        /// Location to execute the command
        /// </summary>
        [JsonProperty("workingDirectory")]
        public string WorkingDirectory { get; set; }

        /// <summary>
        /// Flag to control whether to run command with a shell
        /// </summary>
        [JsonProperty("runWithShell")]
        public bool RunWithShell { get; set; }

        /// <summary>
        /// Flag to indicate command should not remain attached
        /// </summary>
        [JsonProperty("isDetach")]
        public bool IsDetach { get; set; }

        /// <summary>
        /// Command will detach when this string appears in command output
        /// </summary>
        [JsonProperty("detachAfter")]
        public string DetachAfter { get; set; }
    }
}