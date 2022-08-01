// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Threading;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// A base class for applications
    /// </summary>
    internal abstract class AppBase
    {
        public abstract int Execute(string[] args, CancellationToken cancellationToken);
    }
}