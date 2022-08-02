// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.Net.Http.Headers;
using static Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// NOTE IMPORTANT!!:
    /// Changes to this file need to be propagated to the data pipeline:
    /// Pipeline scripts and dataset schemas depend, and are consistent, with the naming given here.
    /// Changes to this strings should be done only if absolutely required, and need to be coordinated with pipeline changes.
    /// </summary>
    public static class LoggingConstants
    {
        internal static class Property
        {
            public const string ApplicationName = "ApplicationName";
            public const string ClientRequestId = "ClientRequestId";
            public const string CommandId = "CommandId";
            public const string DeviceOperatingSystem = "DeviceOperatingSystem";
            public const string Error = "Error";
            public const string ExitCode = "ExitCode";
            public const string Framework = "Framework";
            public const string IsRoutingEnabled = "IsRoutingEnabled";
            public const string MacAddressHash = "MacAddressHash";
            public const string Output = "Output";
            public const string PodCorrelationIds = "PodCorrelationIds";
            public const string ProcessId = "ProcessId";
            public const string Region = "Region";
            public const string TargetCluster = "TargetCluster";
            public const string TargetEnvironment = "TargetEnvironment";
            public const string UserAgent = "UserAgent";
            public const string IsManagedIdentityEnabled = "IsManagedIdentityEnabled";
        }

        internal const string CorrelationIdSeparator = ":";

        internal static class Dependency
        {
            public const string GetAADToken = "GetAADToken";
            public const string Kubernetes = "Kubernetes";
        }

        /// <summary>
        /// This is a list of the possible auth headers we might encounter and that we must not log.
        /// </summary>
        public static readonly string[] HeadersToScramble = { HeaderNames.Authorization, CustomHeaderNames.ClientCertificate };

        /// <summary>
        /// This is a list of common request headers that we don't mark as PII
        /// </summary>
        public static readonly string[] AllowedNonPIIRequestHeaders =
        {
            HeaderNames.Accept,
            HeaderNames.AcceptEncoding,
            HeaderNames.AcceptLanguage,
            HeaderNames.Authority,
            HeaderNames.CacheControl,
            HeaderNames.Connection,
            HeaderNames.ContentEncoding,
            HeaderNames.ContentLanguage,
            HeaderNames.ContentLength,
            HeaderNames.ContentType,
            HeaderNames.Date,
            HeaderNames.Host,
            HeaderNames.MaxForwards,
            HeaderNames.Method,
            HeaderNames.RetryAfter,
            HeaderNames.Scheme,
            HeaderNames.TransferEncoding,
            HeaderNames.Upgrade,
            HeaderNames.UserAgent
        };

        internal static class ClientNames
        {
            public const string MindaroCli = "MindaroCli";
            public const string Library = "Library";
            public const string EndpointManager = "EndpointManager";
            public const string EndpointManagerLauncher = "EndpointManagerLauncher";
            public const string LocalAgent = "LocalAgent";
            public const string RestorationJob = "RestorationJob";
            public const string RoutingManager = "RoutingManager";
            public const string RemoteAgent = "RemoteAgent";
        }
    }
}