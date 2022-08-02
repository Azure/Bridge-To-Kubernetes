// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    // Named "CommonEvents" to prevent conflicting with other project's "Events" class
    // Intended to be used only within common project
    internal static class CommonEvents
    {
        public static class WorkloadRestorationService
        {
            public const string AreaName = nameof(WorkloadRestorationService);

            public static class Operations
            {
                public const string RestorePod = nameof(RestorePod);
                public const string RemovePodDeployment = nameof(RemovePodDeployment);
                public const string RestoreDeployment = nameof(RestoreDeployment);
                public const string RestoreStatefulSet = nameof(RestoreStatefulSet);
            }
        }

        public static class RemoteRestoreJobCleaner
        {
            public const string AreaName = nameof(RemoteRestoreJobCleaner);

            public static class Operations
            {
                public const string Cleanup = nameof(Cleanup);
            }
        }

        public static class IP
        {
            public const string AreaName = nameof(IP);

            public static class Operations
            {
                public const string AllocateIP = nameof(AllocateIP);
                public const string ReleaseIP = nameof(ReleaseIP);
                public const string RemoveRoutingRules = nameof(RemoveRoutingRules);
                public const string AddRoutingRules = nameof(AddRoutingRules);
                public const string Cleanup = nameof(Cleanup);
            }
        }
    }
}