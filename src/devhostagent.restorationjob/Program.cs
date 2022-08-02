// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob
{
    public class Program
    {
        public static int Main(string[] args)
            => ConsoleRunner.RunApp<RestorationJobApp>(
                containerFunc: AppContainerConfig.BuildContainer,
                args: args,
                userAgent: $"{ClientNames.RestorationJob}/{AssemblyVersionUtilities.GetEntryAssemblyInformationalVersion()}");
    }
}