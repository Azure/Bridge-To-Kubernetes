// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.EndpointManager.Logging
{
    internal static class Events
    {
        public static class EndpointManager
        {
            public const string AreaName = "EndpointManager";

            public static class Operations
            {
                public const string EnsureHostsFileAccess = "EnsureHostsFileAccess";
                public const string SystemCheck = "SystemCheck";
                public const string RunAsync = "RunAsync";
                public const string AddHostsFileEntries = "AddHostsFileEntries";
                public const string Cleanup = "Cleanup";
                public const string AllocateIP = "AllocateIP";
                public const string AddRoutingRules = "AddRoutingRules";
                public const string RemoveRoutingRules = "RemoveRoutingRules";
                public const string ReleaseIP = "ReleaseIP";
                public const string GetCurrentServiceIpMap = "GetCurrentServiceIpMap";
                public const string KillProcess = "KillProcess";
                public const string DisableService = "DisableService";
            }
        }
    }
}