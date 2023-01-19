// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.DevHostAgent
{
    internal static class DevHostConstants
    {
        internal static class DevHostAgent
        {
            public const int Port = 50052;
        }

        internal static class DevHostRestorationJob
        {
            public const string ObjectNameLower = "lpkrestorationjob";
            public const string PatchStateFullPath = "/etc/patchstate/patch.json";
        }
    }
}