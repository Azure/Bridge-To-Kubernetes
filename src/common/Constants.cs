// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common
{
    internal static class Constants
    {
        internal static class ApplicationInsights
        {
            internal static class EnvironmentKeys
            {
                public static readonly Guid Production = Guid.Parse("a54a89e1-7d59-4076-a7d0-f0023d4ac07c");
                public static readonly Guid Staging = Guid.Parse("677cdfc6-b4f8-4a3c-a30f-a9825fd900fc");
                public static readonly Guid Development = Guid.Parse("677cdfc6-b4f8-4a3c-a30f-a9825fd900fc");
            }

#if TELEMETRY_STAGING
            internal static readonly string InstrumentationKey = EnvironmentKeys.Staging.ToString();
#elif TELEMETRY_PRODUCTION
            internal static readonly string InstrumentationKey = EnvironmentKeys.Production.ToString();
#elif TELEMETRY_DEVELOPMENT
            internal static readonly string InstrumentationKey = EnvironmentKeys.Development.ToString();
#else
            internal static readonly string InstrumentationKey = EnvironmentKeys.Development.ToString();
#endif
        }

        internal static class Product
        {
            public const string Name = "Bridge To Kubernetes";
            public const string NameAbbreviation = "bridge";
        }

        /// <summary>
        /// The only headers listed here should be custom headers. If you need a
        /// common header, use Microsoft.Net.Http.Headers.HeaderNames instead.
        /// </summary>
        internal static class CustomHeaderNames
        {
            public const string ClientCertificate = "x-ARR-ClientCert";
            public const string ClientRequestId = "x-ms-client-request-id";
            public const string CorrelationRequestId = "x-ms-correlation-request-id";
            public const string RequestId = "x-ms-request-id";
        }

        internal static class DirectoryName
        {
            public const string Logs = Product.Name;
            public static readonly string PersistedFiles = $".{Product.NameAbbreviation}";
        }

        internal static class FileNames
        {
            public const string Config = "config.json";
        }

        internal static class LocalConnect
        {
            public static readonly string KubeConfigPath = $"/{Product.NameAbbreviation}-kubeconfig/config";
            public static readonly string COMMAND_SUCCESS = "#SUCCESS#";
        }

        internal static class EndpointManager
        {
            public const string ProcessName = "EndpointManager";
            public const string DirectoryName = ProcessName;
            public static string SocketName = $"{ProcessName}Socket";
            public static string SocketHandshake = $"{ProcessName} accepted connection";
            public const int SocketBufferSize = 8192; // This is the default size
            public const string EndMarker = "<EOF>";
            public const string LauncherProcessName = "EndpointManagerLauncher";
            public const string LauncherDirectoryName = LauncherProcessName;

            internal static class KnownProcesses
            {
                public const string BranchCacheDisplayName = "BranchCache";
                public const string BranchCacheServiceName = "PeerDistSvc";
            }

            /// <summary>
            /// A dictionary of non critical services for the OS that are safe to kill with their commonly used ports.
            /// </summary>
            public static IDictionary<string, int[]> NonCriticalWindowsPortListeningServices => new Dictionary<string, int[]>()
            {
                { KnownProcesses.BranchCacheDisplayName, new int[] { 80 } },
                { "Internet Information Server", new int[] { 80 } },
                { "SQL Server Reporting Services", new int[] { 80 } },
                { "Sync Share Service", new int[] { 80 } },
                { "Web Deployment Agent Service", new int[] { 80 } },
                { "World Wide Web Publishing Service", new int[] { 80 } }
            };

            internal enum ApiNames
            {
                AddHostsFileEntry,
                AllocateIP,
                DisableService,
                FreeIP,
                KillProcess,
                Ping,
                SystemCheck,
                Stop,
                Version
            }

            internal enum Errors
            {
                UserVisible,
                InvalidOperation
            }
        }

        internal enum ExitCode
        {
            Success = 0,
            Fail = 1,
            Cancel = 2,
            ForceTerminate = 15,
            Timeout = 124
        }

        internal static class Routing
        {
            public const string RoutingLabelPrefix = "routing.visualstudio.io/";
            public const string RouteOnHeaderAnnotationName = RoutingLabelPrefix + "route-on-header";
            public const string DebuggedContainerNameAnnotationName = RoutingLabelPrefix + "debugged-container-name";
            public const string FeatureFlagsAnnotationName = RoutingLabelPrefix + "feature-flags";
            public const string RoutingComponentLabel = RoutingLabelPrefix + "component";
            public const string RouteFromLabelName = RoutingLabelPrefix + "route-from";
            public const string KubernetesRouteAsHeaderName = "kubernetes-route-as";
            public const string RoutingManagerNameLower = "routingmanager";
            public const string RoutingManagerServiceName = RoutingManagerNameLower + "-service";
            public const int RoutingManagerPort = 8766;

            // Annotation used to store the service's original label selector in the service object when the service's label selector is modified
            // This annotation will be used to bring the user service back to its original state when triggers disappear
            public const string OriginalServiceSelectorAnnotation = RoutingLabelPrefix + "originalServiceSelector";

            // This is the common label applied to all generated objects
            public const string GeneratedLabel = RoutingLabelPrefix + "generated";

            public const string InvalidValueOfTriggerError = "Invalid value of trigger ";
        }

        internal static class Labels
        {
            public const string VersionLabelName = "mindaro.io/version";
            public const string ComponentLabelName = "mindaro.io/component";
            public const string InstanceLabelName = "mindaro.io/instance";
            public const string ConnectCloneLabel = "mindaro.io/connect-clone";
        }

        internal static class Annotations
        {
            public const string CorrelationId = "mindaro.io/correlation-id";
            public const string IstioInjection = "sidecar.istio.io/inject";
        }

        internal static class CommandOptions
        {
            public static class OutputType
            {
                public const string Option = "--output";
                public const string Description = "Output format. Allowed values: json, table. Default: table.";
            }

            public static class Quiet
            {
                public const string Option = "--quiet";
                public const string Description = "Output no information during command execution";
            }

            public static class Verbose
            {
                public const string Option = "--verbose";
                public const string Description = "Output more information during command execution";
            }
        }

        internal static class IP
        {
            public const string StartingIP = "127.1.1.0";  // We should always start allocating at StartingIP + 1. That way we can use this IP to test for free ports, as it will never be used by any running process

            /*
             * This port will be the starting port on Linux & Mac to map remote
             * container ports to local ports when Endpoint Manager is used.
             * In non-admin scenarios when EPM is not used, this port will be
             * used as a starting port for all platforms.
             */
            public const int RemoteServicesLocalStartingPort = 55049;

            public const int RemoteServicesLocalEndingPort = 65535;

            public const int PortPlaceHolder = -1;
        }

        internal static class Troubleshooting
        {
            public const string FailedToLoadKubeConfigLink = "https://aka.ms/load-kubeconfig-failed";
        }

        internal static class ManagedIdentity
        {
            public const string MSI_ENDPOINT_EnvironmentVariable = "MSI_ENDPOINT";
            public const string MSI_SECRET_EnvironmentVariable = "MSI_SECRET";

            // This is a dummy value we just set to MSI_SECRET environment variable
            public const string SecretValue = "placeholder";
            public const string EndpointValue = "http://" + TargetServiceNameOnLocalMachine + "/metadata/identity/oauth2/token";

            public const string TargetServiceNameOnLocalMachine = "managedidentityforbridgetokubernetes";
        }

        internal static class ImageName
        {
            public const string RemoteAgentImageName = "lpkremoteagent";
            public const string RestorationJobImageName = "lpkrestorationjob";
        }

        internal static class ImageTag
        {
            public const string RoutingManagerImageTag = "stable";
        }

        internal static class Architecture
        {
            public const string Arm64 = "arm64";
            public const string Amd64 = "amd64";
        }

        internal static class Https
        {
            public const string AcmePath = "/.well-known/acme-challenge";
        }

        internal static class LocalAgent
        {
            public const string LocalAgentConfigPath = "/etc/localAgent/localAgentConfig.json";
            public const string KubeConfigPath = "/etc/localAgent/kubeconfig";
            public const int Port = 7891;
        }

        internal const string DAPR = "DAPR";
    }
}