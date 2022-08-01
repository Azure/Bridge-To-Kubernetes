// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using YamlDotNet.Serialization;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    /// <summary>
    /// Object model for an environment variable in a Kubernetes local process configuration file
    /// </summary>
    internal class LocalProcessConfigFile_EnvVar
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; }

        [YamlMember(Alias = "value")]
        public string Value { get; set; }
    }
}