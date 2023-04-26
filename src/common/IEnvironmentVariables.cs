// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Globalization;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common
{
    internal interface IEnvironmentVariables
    {
        ReleaseEnvironment ReleaseEnvironment { get; }

        LoggingVerbosity ConsoleLoggingVerbosity { get; }

        LoggingVerbosity LogFileVerbosity { get; }

        LoggingVerbosity TelemetryVerbosity { get; }

        string Home { get; }

        string UserName { get; }

        string UserProfile { get; }

        string DevHostImageName { get; }

        string DevHostRestorationJobImageName { get; }

        string RoutingManagerImageName { get; }

        string KubernetesInClusterConfigOverwrite { get; }

        string KubeConfig { get; }

        string SourceUserAgent { get; }

        string CorrelationId { get; }

        bool CollectTelemetry { get; }

        bool EnableLogFile { get; }

        CultureInfo Culture { get; }

        string KubernetesServiceHost { get; }

        string KubernetesServicePort { get; }

        string DotNetRoot { get; }

        bool IsCodespaces { get; }

        string KubectlProxy { get; }
    }
}