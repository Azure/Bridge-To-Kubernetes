// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Different levels of detail to output in logs
    /// </summary>
    public enum LoggingVerbosity
    {
        /// <summary>
        /// Logs only events with EventLevel of LogAlways, Critical, or Error
        /// </summary>
        Quiet,

        /// <summary>
        /// Logs everything included in Quiet, plus Warning and Informational events
        /// </summary>
        Normal,

        /// <summary>
        /// Logs all events
        /// </summary>
        Verbose
    }
}