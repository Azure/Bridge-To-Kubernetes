// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// SerializedProgress class is meant to provide single-threaded-like behavior when using with IProgress.
    /// The built-in Progress class uses current SynchronizationContext to post handlers. If the current
    /// SynchronizationContext is not set, it uses thread pool. This cases the handler being invoked in random
    /// worker threads, and could be undesirable in a CLI environment. In places where we need to ensure
    /// serialzied behavior around IProgress, use this class instead.
    /// </summary>
    internal class SerializedProgress<T> : IProgress<T>
    {
        private Action<T> _handler;

        public SerializedProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            _handler(value);
        }
    }
}