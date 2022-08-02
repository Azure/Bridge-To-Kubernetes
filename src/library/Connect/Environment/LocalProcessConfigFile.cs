// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    /// <summary>
    /// Object model representing a Kubernetes local process configuration file
    /// </summary>
    internal class LocalProcessConfigFile
    {
        [YamlMember(Alias = "version", SerializeAs = typeof(string))]
        public Version Version { get; set; }

        [YamlMember(Alias = "env")]
        public IList<LocalProcessConfigFile_EnvVar> EnvironmentVariables { get; set; }

        [YamlMember(Alias = "volumeMounts")]
        public IList<LocalProcessConfigFile_VolumeMount> VolumeMounts { get; set; }

        [YamlMember(Alias = "enableFeatures")]
        public IList<string> EnableFeatures { get; set; }
    }
}