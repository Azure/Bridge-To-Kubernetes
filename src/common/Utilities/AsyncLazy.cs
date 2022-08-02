// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace System
{
    internal class AsyncLazy<T>
    {
        /// <summary>
        /// The synchronization object protecting _instance.
        /// </summary>
        private readonly object _instanceMutex;

        /// <summary>
        /// The factory method to call.
        /// </summary>
        private readonly Func<Task<T>> _factory;

        /// <summary>
        /// The underlying lazy task.
        /// </summary>
        private Lazy<Task<T>> _instance;

        public AsyncLazy(Func<T> valueFactory, bool retryOnFailure = true) : this(() => Task.Run(valueFactory), retryOnFailure)
        {
        }

        // We run the taskFactory in a separate thread so that there is significant work to be done before the Task is returned it doesn't lock the current thread.
        public AsyncLazy(Func<Task<T>> taskFactory, bool retryOnFailure = true)
        {
            if (taskFactory == null)
            {
                throw new ArgumentNullException(nameof(taskFactory));
            }

            _factory = taskFactory;

            if (retryOnFailure)
            {
                _factory = RetryOnFailure(_factory);
            }

            _instanceMutex = new object();
            _instance = new Lazy<Task<T>>(_factory);
        }

        // This allows calling await on this class
        public TaskAwaiter<T> GetAwaiter()
        {
            lock (_instanceMutex)
            {
                return _instance.Value.GetAwaiter();
            }
        }

        private Func<Task<T>> RetryOnFailure(Func<Task<T>> factory)
        {
            return async () =>
            {
                try
                {
                    return await factory().ConfigureAwait(false);
                }
                catch
                {
                    lock (_instanceMutex)
                    {
                        _instance = new Lazy<Task<T>>(_factory);
                    }
                    throw;
                }
            };
        }
    }
}