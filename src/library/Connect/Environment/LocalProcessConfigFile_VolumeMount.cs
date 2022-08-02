// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    /// <summary>
    /// Object model for volume mount entries in a Kubernetes local process configuration file
    /// </summary>
    internal class LocalProcessConfigFile_VolumeMount
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "localPath")]
        public string LocalPath { get; set; }
    }
}