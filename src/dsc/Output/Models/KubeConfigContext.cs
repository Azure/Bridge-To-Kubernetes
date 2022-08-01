// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Exe.Output.Models
{
    internal class KubeConfigContext
    {
        public bool Current { get; }
        public string Name { get; }
        public string Cluster { get; }
        public string Server { get; }
        public string User { get; }
        public string Namespace { get; }

        public KubeConfigContext(bool current, string name, string cluster, string server, string user, string namespaceName)
        {
            this.Current = current;
            this.Name = name;
            this.Cluster = cluster;
            this.Server = server;
            this.User = user;
            this.Namespace = namespaceName;
        }
    }
}