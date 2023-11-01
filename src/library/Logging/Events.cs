// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Logging
{
    internal static class Events
    {
        public static class KubernetesManagementClient
        {
            public const string AreaName = "KubernetesManagementClient";

            public static class Operations
            {
                public const string CheckCredentialsAsync = "CheckCredentialsAsync";
                public const string ListNamespacesAsync = "ListNamespacesAsync";
                public const string ListServicesInNamespacesAsync = "ListServicesInNamespacesAsync";
                public const string ListPublicUrlsInNamespaceAsync = "ListPublicUrlsInNamespaceAsync";
                public const string GetRoutingHeaderValue = "GetRoutingHeaderValue";
                public const string IsRoutingSupported = "IsRoutingSupported";
                public const string RefreshCredentialsAsync = "RefreshCredentialsAsync";
            }
        }

        public static class ConnectManagementClient
        {
            public const string AreaName = "ConnectManagementClient";

            public static class Operations
            {
                public const string AddLocalMappings = "AddLocalMappings";
                public const string ConfigureLocalHostAsync = "ConfigureLocalHost";
                public const string StartEndpointManagerAsync = "StartEndpointManager";
                public const string GetElevationRequestsAsync = "GetElevationRequests";
                public const string GetWorkloadInfo = "GetContainerWorkloadInfo";
                public const string StopLocalConnectionAsync = "StopLocalConnection";
                public const string RestoreOriginalRemoteContainerAsync = "RestoreOriginalRemoteContainer";
                public const string StartRemoteAgentAsync = "StartRemoteAgent";
                public const string StartLocalAgentAsync = "StartLocalAgent";
                public const string StopLocalAgentAsync = "StopLocalAgent";
                public const string ConnectToRemoteAgent = "ConnectToRemoteAgent";
                public const string WaitRemoteAgentChangeAsync = "WaitRemoteAgentChange";
                public const string StartServicePortForwardingsAsync = "StartServicePortForwardings";
            }
        }

        public static class RoutingManagementClient
        {
            public const string AreaName = "RoutingManagementClient";

            public static class Operations
            {
                public const string DeployRoutingManager = "DeployRoutingManager";
                public const string GetStatus = "GetStatus";
            }
        }

        public static class KubernetesRemoteEnvironmentManager
        {
            public const string AreaName = "KubernetesRemoteEnvironmentManager";

            public static class Operations
            {
                public const string DeployAgentOnlyPod = "DeployAgentOnlyPod";
                public const string PatchDeployment = "PatchDeployment";
                public const string PatchStatefulSet = "PatchStatefulSet";
                public const string Restore = "Restore";
                public const string StartRemoteAgent = "StartRemoteAgent";
                public const string GetPodsFromService = "GetPodsFromService";
                public const string WaitForWorkloadStopped = "WaitForWorkloadStopped";
                public const string RestoreRemoteAgent = "RestoreRemoteAgent";
            }
        }

        public static class WorkloadInformationProvider
        {
            public const string AreaName = "WorkloadInformationProvider";

            public static class Operations
            {
                public const string GetReachableEndpoints = "GetReachableEndpoints";
                public const string GatherWorkloadInfo = "GatherWorkloadInfo";
            }
        }

        public static class LocalEnvironmentManager
        {
            public const string AreaName = "LocalEnvironmentManager";

            public static class Operations
            {
                public const string AddLocalMappings = "AddLocalMappings";
                public const string AddLocalMappingsUsingClusterEnvironmentVariables = "AddLocalMappingsUsingClusterEnvironmentVariables";
                public const string StartServicePortForwardings = "StartServicePortForwardings";
                public const string StartReversePortForwarding = "StartReversePortForwarding";
                public const string StartLocalAgent = "StartLocalAgent";
                public const string StopLocalAgent = "StopLocalAgent";
                public const string StartNonAdminServicePortForwardings = "StartNonAdminServicePortForwardings";
                public const string GetLocalEnvironment = "GetLocalEnvironment";
                public const string StopWorkload = "StopWorkload";
                public const string SatisfyEnvFile = "SatisfyEnvFile";
            }
        }

        public static class RemoteRestoreJobDeployer
        {
            public const string AreaName = "RemoteRestoreJobDeployer";

            public static class Operations
            {
                public const string Deploy = "Deploy";
                public const string EnsureRbacResources = "EnsureRbacResources";
                public const string TryGetExistingRestoreJobInfo = "TryGetExistingRestoreJobInfo";
            }
        }
    }
}