// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob.Logging
{
    internal static class Events
    {
        public static class RestorationJob
        {
            public const string AreaName = "RestorationJob";

            public static class Operations
            {
                public const string AgentPing = nameof(AgentPing);
                public const string Restore = nameof(Restore);
            }

            public static class Properties
            {
                public const string RestorePerformed = nameof(RestorePerformed);
                public const string HasConnectedClients = nameof(HasConnectedClients);
                public const string NumFailedPings = nameof(NumFailedPings);
            }
        }
    }
}