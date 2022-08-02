// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.BridgeToKubernetes.TestHelpers
{
    /// <summary>
    /// Determines <see cref="ParameterInfo"/> equality
    /// </summary>
    public class ParameterInfoComparer : IEqualityComparer<ParameterInfo>
    {
        public bool Equals(ParameterInfo x, ParameterInfo y)
        {
            if (x.Name != y.Name)
            {
                return false;
            }
            if (x.ParameterType != y.ParameterType)
            {
                return false;
            }
            if (x.Position != y.Position)
            {
                return false;
            }
            if (x.HasDefaultValue)
            {
                if (!y.HasDefaultValue || (dynamic)x.DefaultValue != (dynamic)y.DefaultValue)
                {
                    return false;
                }
            }
            if (x.IsOut != y.IsOut)
            {
                return false;
            }

            return true;
        }

        public int GetHashCode(ParameterInfo obj)
        {
            return $"{obj.ParameterType.GetHashCode()}{obj.Name}{obj.Position}{obj.HasDefaultValue}{obj.DefaultValue ?? "null"}{obj.IsOut}".GetHashCode();
        }
    }
}