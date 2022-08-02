// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Diagnostics.Tracing;

namespace Microsoft.BridgeToKubernetes.Common.Models
{
    /// <summary>
    /// Information to display to the user regarding progress of an operation
    /// </summary>
    public class ProgressMessage
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="level">Severity of the message</param>
        /// <param name="message">Text to display to the user</param>
        /// <param name="newLine">Indicates whether a newline character should be appended to the message</param>
        public ProgressMessage(EventLevel level, string message, bool newLine = true)
        {
            this.Level = level;
            this.Message = message;
            this.NewLine = newLine;
        }

        /// <summary>
        /// Severity of the message
        /// </summary>
        public EventLevel Level { get; }

        /// <summary>
        /// Text to display to the user
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Indicates whether a newline character should be appended to the message
        /// </summary>
        public bool NewLine { get; }
    }
}