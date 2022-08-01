// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.RoutingManager.Logging
{
    internal static class Events
    {
        public static class RoutingManager
        {
            public const string AreaName = "RoutingManager";

            public static class Operations
            {
                public const string RefreshLoop = "RefreshLoop";
                public const string CreateTriggers = "CreateTriggers";
                public const string GenerateResources = "GenerateResources";
                public const string UpdateClusterState = "UpdateClusterState";
                public const string RedirectTrafficThroughEnvoyPod = "RedirectTrafficThroughEnvoyPod";
                public const string TreatDanglingServices = "TreatDanglingServices";
                public const string Https = "Https";
            }
        }
    }
}