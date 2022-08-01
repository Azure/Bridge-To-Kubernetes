// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob
{
    internal interface IRestorationJobEnvironmentVariables
    {
        /// <summary>
        /// The current namespace
        /// </summary>
        string Namespace { get; }

        /// <summary>
        /// The value of the instance label this instance is tagged with
        /// </summary>
        string InstanceLabelValue { get; }

        /// <summary>
        /// How long there should be zero clients connected before restoring state
        /// </summary>
        TimeSpan RestoreTimeout { get; }

        /// <summary>
        /// The sleep interval between agent pings
        /// </summary>
        TimeSpan PingInterval { get; }

        /// <summary>
        /// Number of consecutive failed cycles before the container should exit
        /// </summary>
        int NumFailedPingsBeforeExit { get; }
    }
}