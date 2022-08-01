// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// Provides helpers to safely run Async methods synchronously, by creating an isolated synchronization context (helps prevent stupid deadlocks)
    /// </summary>
    internal static class AsyncHelpers
    {
        /// <summary>
        /// Executes an async Task method which has a void return value synchronously
        /// </summary>
        /// <param name="task">
        /// Task method to execute.
        /// DO NOT use implicitly passed thread-control types (such as CancellationToken) in the Func. We have seen issues with deadlocks when
        /// the async Func (running on a new Task SynchronizationContext), attempts to access CancellationTokens owned by the main SynchronizationContext.
        /// In these scenarios, prefer Task.ConfigureAwait(false).GetAwaiter().GetResult(). The "false" parameter means "don't try to marshal
        /// the continuation back to the original context captured".
        /// </param>
        /// <remarks>The task target must not be null e.g. object?.DoThis() will throw if object is null</remarks>
        /// <exception cref="NullReferenceException">If the Task target is null</exception>
        public static void RunSync(Func<Task> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            synch.Post(async _ =>
            {
                try
                {
                    await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);

            try
            {
                synch.BeginMessageLoop();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }
        }

        /// <summary>
        /// Execute's an async Task{T} method which has a T return type synchronously
        /// </summary>
        /// <typeparam name="T">Return Type</typeparam>
        /// <param name="task">
        /// Task{T} method to execute.
        /// DO NOT use implicitly passed thread-control types (such as CancellationToken) in the Func. We have seen issues with deadlocks when
        /// the async Func (running on a new Task SynchronizationContext), attempts to access CancellationTokens owned by the main SynchronizationContext.
        /// In these scenarios, prefer Task.ConfigureAwait(false).GetAwaiter().GetResult(). The "false" parameter means "don't try to marshal
        /// the continuation back to the original context captured".
        /// </param>
        /// <returns></returns>
        public static T RunSync<T>(Func<Task<T>> task)
        {
            var oldContext = SynchronizationContext.Current;
            var synch = new ExclusiveSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(synch);
            T ret = default(T);
            synch.Post(async _ =>
            {
                try
                {
                    ret = await task();
                }
                catch (Exception e)
                {
                    synch.InnerException = e;
                    throw;
                }
                finally
                {
                    synch.EndMessageLoop();
                }
            }, null);

            try
            {
                synch.BeginMessageLoop();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(oldContext);
            }

            return ret;
        }

        /// <summary>
        /// A class that represents an "exclusive execution" context, meaning it can only be used by the creator
        /// </summary>
        private class ExclusiveSynchronizationContext : SynchronizationContext
        {
            private bool done;
            public Exception InnerException { get; set; }
            private readonly AutoResetEvent workItemsWaiting = new AutoResetEvent(false);

            private readonly Queue<Tuple<SendOrPostCallback, object>> items =
                new Queue<Tuple<SendOrPostCallback, object>>();

            /// <summary>
            /// Not supported
            /// </summary>
            /// <param name="d"></param>
            /// <param name="state"></param>
            public override void Send(SendOrPostCallback d, object state)
            {
                throw new NotSupportedException("We cannot send to our same thread");
            }

            /// <summary>
            /// Adds work to this synchronization context
            /// </summary>
            /// <param name="d"></param>
            /// <param name="state"></param>
            public override void Post(SendOrPostCallback d, object state)
            {
                lock (items)
                {
                    items.Enqueue(Tuple.Create(d, state));
                }
                workItemsWaiting.Set();
            }

            /// <summary>
            /// This is an exclusive context, so creating copies is not supported. Simply returns 'this'.
            /// </summary>
            /// <returns></returns>
            public override SynchronizationContext CreateCopy()
            {
                // Disable copies
                return this;
            }

            /// <summary>
            /// Breaks the blocking loop in BeginMessageLoop()
            /// </summary>
            public void EndMessageLoop()
            {
                Post(_ => done = true, null);
            }

            /// <summary>
            /// Begins processing messages for this context. Blocks until EndMessageLoop() is called.
            /// </summary>
            public void BeginMessageLoop()
            {
                while (!done)
                {
                    Tuple<SendOrPostCallback, object> task = null;
                    lock (items)
                    {
                        if (items.Count > 0)
                        {
                            task = items.Dequeue();
                        }
                    }
                    if (task != null)
                    {
                        task.Item1(task.Item2);
                        if (InnerException != null) // the method threw an exception
                        {
                            ExceptionDispatchInfo.Capture(InnerException).Throw();
                        }
                    }
                    else
                    {
                        workItemsWaiting.WaitOne();
                    }
                }
            }
        }
    }
}