// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    /// <summary>
    /// Manages an iteration tracking session. Stops tracking when disposed.
    /// </summary>
    internal class IterationTrackingSession : IDisposable
    {
        private Action _stop;

        internal IterationTrackingSession(Action stop)
        {
            _stop = stop;
        }

        public void Dispose()
        {
            _stop?.Invoke();
        }
    }
}