// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// Information about the process occupying a port
    /// </summary>
    public class ProcessPortMapping : IPortMapping
    {
        /// <summary>
        /// Represents a process occupying a port
        /// </summary>
        [JsonConstructor]
        public ProcessPortMapping(string processName, int portNumber, int processId)
        {
            this.ProcessName = processName;
            this.PortNumber = portNumber;
            this.ProcessId = processId;
        }

        /// <summary>
        /// Name of the process
        /// </summary>
        public string ProcessName { get; }

        /// <summary>
        /// Occupied port number
        /// </summary>
        public int PortNumber { get; }

        /// <summary>
        /// Process ID
        /// </summary>
        public int ProcessId { get; }
    }
}