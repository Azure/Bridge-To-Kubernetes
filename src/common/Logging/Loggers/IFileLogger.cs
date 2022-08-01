// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Interface to retrieve file logger information
    /// </summary>
    internal interface IFileLogger
    {
        /// <summary>
        /// Gets the fully-qualified directory containing the current log file. Returns empty if no log files have been created during the session.
        /// </summary>
        string CurrentLogDirectoryPath { get; }
    }
}