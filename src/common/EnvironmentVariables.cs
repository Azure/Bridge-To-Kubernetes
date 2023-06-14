// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Globalization;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common
{
    internal class EnvironmentVariables : IEnvironmentVariables
    {
        // For some reason the this variable is empty if referenced before it is declared. So this needs to be at the top.
        protected static readonly string BRIDGE = Constants.Product.NameAbbreviation.ToUpperInvariant();

        public static class Names
        {
            public static readonly string BridgeEnvironment = $"{BRIDGE}_ENVIRONMENT";
            public static readonly string ConsoleVerbosity = "CONSOLE_VERBOSITY";
            public static readonly string LogFileVerbosity = "LOG_FILE_VERBOSITY";
            public static readonly string TelemetryVerbosity = "TELEMETRY_VERBOSITY";
            public static readonly string Home = "HOME";
            public static readonly string UserProfile = "USERPROFILE";
            public static readonly string DevHostImageName = $"{BRIDGE}_DEVHOSTIMAGENAME";
            public static readonly string RestorationJobImageName = $"{BRIDGE}_RESTORATIONJOBIMAGENAME";
            public static readonly string RoutingManagerImageName = $"{BRIDGE}_ROUTINGMANAGERIMAGENAME";
            public static readonly string SourceUserAgent = $"{BRIDGE}_SOURCE_USER_AGENT";
            public static readonly string CorrelationId = $"{BRIDGE}_CORRELATION_ID";
            public static readonly string CollectTelemetry = $"{BRIDGE}_COLLECT_TELEMETRY";
            public static readonly string EnableLogFile = $"{BRIDGE}_ENABLE_LOG_FILE";
            public static readonly string Culture = $"{BRIDGE}_CULTURE";
            public static readonly string KubernetesInClusterConfigOverride = "KUBERNETES_IN_CLUSTER_CONFIG_OVERWRITE";
            public static readonly string KubeConfig = "KUBECONFIG";
            public static readonly string KubernetesServiceHost = "KUBERNETES_SERVICE_HOST";
            public static readonly string KubernetesServicePort = "KUBERNETES_SERVICE_PORT";
            public static readonly string DotNetRoot = "DOTNET_ROOT";
            public static readonly string IsCodespaces = $"{BRIDGE}_IS_CODESPACES";
            public static readonly string KubectlProxy = "KUBECTL_PROXY";
        }

        private readonly Lazy<ReleaseEnvironment> _releaseEnvironment = new Lazy<ReleaseEnvironment>(() => GetReleaseEnvironment(Get(Names.BridgeEnvironment)));
        private readonly Lazy<LoggingVerbosity> _consoleLoggingVerbosity;
        private readonly Lazy<LoggingVerbosity> _logFileVerbosity;
        private readonly Lazy<LoggingVerbosity> _telemetryVerbosity;
        private readonly Lazy<string> _home = new Lazy<string>(() => GetRequired(Names.Home));
        private readonly Lazy<string> _userName;
        private readonly Lazy<string> _userProfile = new Lazy<string>(() => GetRequired(Names.UserProfile));
        private readonly Lazy<string> _devHostImageName = new Lazy<string>(() => Get(Names.DevHostImageName));
        private readonly Lazy<string> _devHostRestorationJobImageName = new Lazy<string>(() => Get(Names.RestorationJobImageName));
        private readonly Lazy<string> _routingManagerImageName = new Lazy<string>(() => Get(Names.RoutingManagerImageName));
        private readonly Lazy<string> _sourceUserAgent = new Lazy<string>(() => Get(Names.SourceUserAgent));
        private readonly Lazy<string> _correlationId = new Lazy<string>(() => Get(Names.CorrelationId));
        private readonly Lazy<bool> _collectTelemetry = new Lazy<bool>(() => GetBool(Names.CollectTelemetry, defaultValue: false));
        private readonly Lazy<bool> _enableLogFile = new Lazy<bool>(() => GetBool(Names.EnableLogFile, defaultValue: true));
        private readonly Lazy<CultureInfo> _culture = new Lazy<CultureInfo>(() => GetCultureInfo(Names.Culture));
        private readonly Lazy<string> _kubernetesInClusterConfigOverwrite = new Lazy<string>(() => Get(Names.KubernetesInClusterConfigOverride));
        private readonly Lazy<string> _kubeConfig = new Lazy<string>(() => Get(Names.KubeConfig));
        private readonly Lazy<string> _kubernetesServiceHost = new Lazy<string>(() => Get(Names.KubernetesServiceHost));
        private readonly Lazy<string> _kubernetesServicePort = new Lazy<string>(() => Get(Names.KubernetesServicePort));
        private readonly Lazy<string> _dotNetRoot = new Lazy<string>(() => Get(Names.DotNetRoot));
        private readonly Lazy<bool> _isCodespaces = new Lazy<bool>(() => GetBool(Names.IsCodespaces, defaultValue: false));
        private readonly Lazy<string> _kubectlProxy = new Lazy<string>(() => Get(Names.KubectlProxy));

        public EnvironmentVariables(IPlatform platform)
        {
            _userName = new Lazy<string>(() => platform.IsWindows ? GetRequired("USERNAME") : GetRequired("USER"));
            _consoleLoggingVerbosity = new Lazy<LoggingVerbosity>(() => GetLoggingVerbosity(Names.ConsoleVerbosity, defaultProductionVerbosity: LoggingVerbosity.Normal));
            _logFileVerbosity = new Lazy<LoggingVerbosity>(() => GetLoggingVerbosity(Names.LogFileVerbosity, defaultProductionVerbosity: LoggingVerbosity.Verbose));
            _telemetryVerbosity = new Lazy<LoggingVerbosity>(() => GetLoggingVerbosity(Get(Names.TelemetryVerbosity), defaultProductionVerbosity: LoggingVerbosity.Verbose));
        }

        #region IEnvironmentVariables

        public virtual ReleaseEnvironment ReleaseEnvironment => _releaseEnvironment.Value;

        public virtual string Home => _home.Value;

        public virtual string UserName => _userName.Value;

        public virtual string UserProfile => _userProfile.Value;

        public virtual string DevHostImageName => _devHostImageName.Value;

        public virtual string DevHostRestorationJobImageName => _devHostRestorationJobImageName.Value;

        public virtual string RoutingManagerImageName => _routingManagerImageName.Value;

        public virtual LoggingVerbosity ConsoleLoggingVerbosity => _consoleLoggingVerbosity.Value;

        public virtual LoggingVerbosity LogFileVerbosity => _logFileVerbosity.Value;

        public virtual LoggingVerbosity TelemetryVerbosity => _telemetryVerbosity.Value;

        public virtual string SourceUserAgent => _sourceUserAgent.Value;

        public virtual string CorrelationId => _correlationId.Value;

        public virtual bool CollectTelemetry => _collectTelemetry.Value;

        public virtual bool EnableLogFile => _enableLogFile.Value;

        public virtual CultureInfo Culture => _culture.Value;

        public virtual string KubernetesInClusterConfigOverwrite => _kubernetesInClusterConfigOverwrite.Value;

        public virtual string KubeConfig => _kubeConfig.Value;

        public virtual string KubernetesServiceHost => _kubernetesServiceHost.Value;

        public virtual string KubernetesServicePort => _kubernetesServicePort.Value;

        public string DotNetRoot => _dotNetRoot.Value;

        public bool IsCodespaces => _isCodespaces.Value;

        public string KubectlProxy => _kubectlProxy.Value;

        #endregion IEnvironmentVariables

        /// <summary>
        /// Gets the value of a system environment variable
        /// </summary>
        /// <param name="name">Environment variable name</param>
        /// <returns>The string value of the environment variable, or null if the variable is not defined</returns>
        protected static string Get(string name)
        {
            var value = string.IsNullOrWhiteSpace(name) ? null : Environment.GetEnvironmentVariable(name);

            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        /// <summary>
        /// Checks if a given environment variable used as a flag has a value indicating it is enabled, like "1" or "true".
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <param name="defaultValue">The default value to use if the environment value is not set or set to an invalid (not bool-like) value</param>
        /// <returns>true if the variable is enabled, false otherwise.</returns>
        protected static bool GetBool(string name, bool defaultValue)
        {
            var value = Get(name);

            // If the environment variable is not set or is empty, return the default value
            if (string.IsNullOrWhiteSpace(value))
            {
                return defaultValue;
            }

            // If the Environment variable exactly matches an expected bool-like value, return the corresponding bool value
            if (value.Equals("1") || StringComparer.OrdinalIgnoreCase.Equals(value, "true") || StringComparer.OrdinalIgnoreCase.Equals(value, "yes") || StringComparer.OrdinalIgnoreCase.Equals(value, "on"))
            {
                return true;
            }

            if (value.Equals("0") || StringComparer.OrdinalIgnoreCase.Equals(value, "false") || StringComparer.OrdinalIgnoreCase.Equals(value, "no") || StringComparer.OrdinalIgnoreCase.Equals(value, "off"))
            {
                return false;
            }

            // The environment variable is set, but not to an expected bool-like value, so return the default value
            return defaultValue;
        }

        /// <summary>
        /// Retrieves an environment variable value and converts it to an int
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, converted to an int, or -1 if the environment variable is not defined or its value cannot be parsed</returns>
        /// <throws>An <see cref="ArgumentNullException"/> if the environment variable is not defined</throws>
        /// <throws>A <see cref="FormatException"/> if the environment variable value cannot be parsed as an int</throws>
        /// <throws>An <see cref="OverflowException"/> if the parsed number would overflow int</throws>
        protected static int GetRequiredInt(string name)
            => int.Parse(GetRequired(name));

        /// <summary>
        /// Retrieves an environment variable value and converts it to an int
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, converted to an int, or null if the environment variable is not defined or its value cannot be parsed</returns>
        protected static int? GetInt(string name)
        {
            if (int.TryParse(Get(name), out int value))
            {
                return value;
            }
            return null;
        }

        /// <summary>
        /// Retrieves an environment variable value and converts it to a double
        /// </summary>
        /// <param name="name">The name of the environment variable</param>
        /// <returns>The value of the environment variable, converted to a double, or null if the environment variable is not defined or its value cannot be parsed</returns>
        protected static double? GetDouble(string name)
            => double.TryParse(Get(name), out double value) ? (double?)value : null;

        /// <summary>
        /// Retrieves an environment variable value and converts it to a double
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, converted to a double</returns>
        /// <throws>An <see cref="ArgumentNullException"/> if the environment variable is not defined</throws>
        /// <throws>A <see cref="FormatException"/> if the environment variable value cannot be parsed as a double</throws>
        /// <throws>An <see cref="OverflowException"/> if the parsed number would overflow double</throws>
        protected static double GetRequiredDouble(string name)
            => double.Parse(GetRequired(name));

        /// <summary>
        /// Retrieves an environment variable value and converts it to a Guid
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        /// <returns>The value of the environment variable, converted to a Guid</returns>
        /// <throws>An <see cref="ArgumentNullException"/> if the environment variable is not defined</throws>
        /// <throws>A <see cref="FormatException"/> if the environment variable value cannot be parsed as a Guid</throws>
        protected static Guid GetRequiredGuid(string name)
            => Guid.Parse(GetRequired(name));

        /// <summary>
        /// Get the value of an environment variable that must be defined
        /// </summary>
        /// <param name="name">Environment variable name</param>
        /// <throws>A <see cref="ConfigurationException"/> if the variable is not set</throws>
        /// <returns>The environment variable's value</returns>
        protected static string GetRequired(string name)
        {
            var value = Get(name);
            if (value == null)
            {
                throw new ConfigurationException(name, "Environment variable is not defined!");
            }

            return value;
        }

        /// <summary>
        /// Gets the current <see cref="Common.ReleaseEnvironment"/> from the environment variable
        /// </summary>
        private static ReleaseEnvironment GetReleaseEnvironment(string name)
        {
            switch (name?.ToLowerInvariant())
            {
                case "local":
                    return ReleaseEnvironment.Local;

                case "development":
                case "dev":
                    return ReleaseEnvironment.Development;

                case "staging":
                case "stage":
                    return ReleaseEnvironment.Staging;

                case "test":
                    return ReleaseEnvironment.Test;

                default:
                    return ReleaseEnvironment.Production;
            }
        }

        /// <summary>
        /// Determines the <see cref="LoggingVerbosity"/> by reading the environment variable named <paramref name="name"/>,
        /// and falling back on smart defaults based on the current <see cref="ReleaseEnvironment"/>
        /// </summary>
        /// <returns>
        /// The <see cref="LoggingVerbosity"/> specified by the <paramref name="name"/> environment variable.
        /// If the <paramref name="name"/> environment variable is not set: in production returns <paramref name="defaultProductionVerbosity"/>,
        /// in other environments returns <see cref="LoggingVerbosity.Verbose"/>
        /// </returns>
        private LoggingVerbosity GetLoggingVerbosity(string name, LoggingVerbosity defaultProductionVerbosity)
        {
            var value = Get(name);
            switch (value?.ToLowerInvariant())
            {
                case "quiet":
                    return LoggingVerbosity.Quiet;

                case "normal":
                    return LoggingVerbosity.Normal;

                case "verbose":
                    return LoggingVerbosity.Verbose;

                default:
                    return this.ReleaseEnvironment.IsProduction() ? defaultProductionVerbosity : LoggingVerbosity.Verbose;
            }
        }

        private static CultureInfo GetCultureInfo(string name)
        {
            var culture = Get(name);
            if (!string.IsNullOrWhiteSpace(culture))
            {
                try
                {
                    return CultureInfo.CreateSpecificCulture(culture);
                }
                catch
                { }
            }

            return CultureInfo.CurrentUICulture;
        }
    }
}