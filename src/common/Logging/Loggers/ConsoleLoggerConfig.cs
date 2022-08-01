// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class ConsoleLoggerConfig : IConsoleLoggerConfig
    {
        private readonly IEnvironmentVariables _environmentVariables;

        public ConsoleLoggerConfig(IEnvironmentVariables environmentVariables, string applicationName)
        {
            _environmentVariables = environmentVariables;
            this.ConsoleLoggingVerbosity = _environmentVariables.ConsoleLoggingVerbosity;
            this.ApplicationName = applicationName;
        }

        public LoggingVerbosity ConsoleLoggingVerbosity { get; set; }

        public string ApplicationName { get; set; }
    }
}