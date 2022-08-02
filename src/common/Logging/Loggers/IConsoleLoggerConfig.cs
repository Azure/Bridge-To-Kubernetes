// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal interface IConsoleLoggerConfig
    {
        /// <summary>
        /// The verbosity of the console logs
        /// </summary>
        LoggingVerbosity ConsoleLoggingVerbosity { get; set; }

        /// <summary>
        /// The name of the application
        /// </summary>
        string ApplicationName { get; set; }
    }
}