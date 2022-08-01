// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal interface IFileLoggerConfig
    {
        /// <summary>
        /// Property to determine if logging to file should be enabled
        /// </summary>
        bool LogFileEnabled { get; set; }

        /// <summary>
        /// The verbosity of the logs
        /// </summary>
        LoggingVerbosity LogFileVerbosity { get; set; }

        /// <summary>
        /// The name of the application
        /// </summary>
        string ApplicationName { get; set; }
    }
}