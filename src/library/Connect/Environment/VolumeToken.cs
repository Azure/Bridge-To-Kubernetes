// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal class VolumeToken : EnvironmentTokenBase, IVolumeToken
    {
        public VolumeToken(string name, string tokenString, string localPath) : base(name, tokenString)
        {
            this.LocalPath = localPath;
        }

        public string LocalPath { get; set; }

        public override string Evaluate()
        {
            return LocalPath;
        }

        public override bool Equals(object obj)
        {
            VolumeToken that = obj as VolumeToken;
            return that != null && (this == that || (this.Name == that.Name && this.LocalPath == that.LocalPath));
        }

        public override int GetHashCode()
        {
            int c = base.GetHashCode();
            if (!string.IsNullOrEmpty(this.Name))
            {
                c ^= this.Name.GetHashCode();
            }
            if (!string.IsNullOrEmpty(this.LocalPath))
            {
                c ^= this.LocalPath.GetHashCode();
            }
            return c;
        }
    }
}