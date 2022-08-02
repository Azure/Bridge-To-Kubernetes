// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal class ServiceToken : EnvironmentTokenBase, IServiceToken
    {
        public ServiceToken(string name, string tokenString, int[] ports) : base(name, tokenString)
        {
            Ports = ports;
        }

        public int[] Ports { get; set; }

        public string IpAddress { get; set; }

        public override string Evaluate()
        {
            return IpAddress;
        }

        public override bool Equals(object obj)
        {
            ServiceToken that = obj as ServiceToken;
            return that != null && (this == that || (StringComparer.OrdinalIgnoreCase.Equals(this.Name, that.Name) && Enumerable.SequenceEqual(this.Ports ?? new int[] { }, that.Ports ?? new int[] { })));
        }

        public override int GetHashCode()
        {
            int c = base.GetHashCode();
            if (!string.IsNullOrEmpty(this.Name))
            {
                c ^= this.Name.GetHashCode();
            }
            if (this.Ports != null)
            {
                Ports.ExecuteForEach(p => c ^= p);
            }
            return c;
        }
    }
}