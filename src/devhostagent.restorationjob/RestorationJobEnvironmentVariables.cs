// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob
{
    internal class RestorationJobEnvironmentVariables : EnvironmentVariables, IRestorationJobEnvironmentVariables
    {
        private readonly Lazy<string> _namespace = new Lazy<string>(() => GetRequired("NAMESPACE"));
        private readonly Lazy<string> _instanceLabelValue = new Lazy<string>(() => GetRequired("INSTANCE_LABEL_VALUE"));
        private readonly Lazy<TimeSpan> _restoreTimeout = new Lazy<TimeSpan>(() => TimeSpan.FromMinutes(GetDouble("RESTORE_TIMEOUT_MINUTES") ?? 120)); // Default 1 min
        private readonly Lazy<TimeSpan> _pingInterval = new Lazy<TimeSpan>(() => TimeSpan.FromSeconds(GetDouble("PING_INTERVAL_SECONDS") ?? 30)); // Default 5 seconds
        private readonly Lazy<int> _numFailedPingsBeforeExit = new Lazy<int>(() => GetInt("NUM_FAILED_PINGS_BEFORE_EXIT") ?? 12);

        public RestorationJobEnvironmentVariables(IPlatform platform)
            : base(platform)
        { }

        public string Namespace => _namespace.Value;
        public string InstanceLabelValue => _instanceLabelValue.Value;
        public TimeSpan RestoreTimeout => _restoreTimeout.Value;
        public TimeSpan PingInterval => _pingInterval.Value;
        public int NumFailedPingsBeforeExit => _numFailedPingsBeforeExit.Value;
    }
}