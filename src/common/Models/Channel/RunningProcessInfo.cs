// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.Channel
{
    /// <summary>
    /// Describes a running process
    /// </summary>
    public class RunningProcessInfo
    {
        /// <summary>
        /// Name of the process
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Unique identifier of the process
        /// </summary>
        public int ProcessId { get; set; }
    }
}