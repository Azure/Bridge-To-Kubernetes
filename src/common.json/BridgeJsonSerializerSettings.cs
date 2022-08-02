// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.BridgeToKubernetes.Common.Json
{
    /// <summary>
    /// A spiritual duplicate of Newtonsoft.Json's JsonSerializerSettings. Prevents us from depending on v11 features, since VS requires us to use v9 still.
    /// </summary>
    /// <remarks>If you make any changes/additions, please update JsonHelpers.ConvertSerializerSettings()</remarks>
    internal class BridgeJsonSerializerSettings
    {
        public int? MaxDepth { get; set; }
        public BridgeReferenceLoopHandling? ReferenceLoopHandling { get; set; }
        public Dictionary<Type, HashSet<string>> Ignores { get; set; }
    }

    /// <remarks>
    /// This enum should mirror Newtonsoft.Json's <see cref="Newtonsoft.Json.ReferenceLoopHandling"/>
    /// </remarks>
    internal enum BridgeReferenceLoopHandling
    {
        Error = 0,
        Ignore = 1,
        Serialize = 2
    }
}