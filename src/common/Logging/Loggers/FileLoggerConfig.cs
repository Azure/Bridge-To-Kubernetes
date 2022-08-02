// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class FileLoggerConfig : IFileLoggerConfig
    {
        private readonly IEnvironmentVariables _environmentVariables;

        public FileLoggerConfig(IEnvironmentVariables environmentVariables, string applicationName)
        {
            _environmentVariables = environmentVariables;
            this.LogFileEnabled = _environmentVariables.EnableLogFile;
            this.LogFileVerbosity = _environmentVariables.LogFileVerbosity;
            this.ApplicationName = applicationName;
        }

        public bool LogFileEnabled { get; set; }

        public LoggingVerbosity LogFileVerbosity { get; set; }

        public string ApplicationName { get; set; }
    }
}