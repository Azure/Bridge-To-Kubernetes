// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// A fake disposable for instances where we want a Dispose() operation to no-op
    /// </summary>
    internal class NoOpDisposable : IDisposable
    {
        public void Dispose()
        {
            // No-op
        }
    }
}