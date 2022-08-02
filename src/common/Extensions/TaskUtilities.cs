// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace System.Threading.Tasks
{
    internal static class TaskUtilities
    {
        /// <summary>
        /// Runs a task, but will immediately throw if cancellation is requested.
        /// Note that the Task will continue to run in the background even if cancellation is requested, hence the "Unsafe" name.
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        /// <param name="t"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static Task RunWithUnsafeCancellationAsync(this Task t, CancellationToken token)
        {
            Func<Task<object>> func = async () =>
            {
                await t;
                return null;
            };
            return RunWithUnsafeCancellationAsync(func(), token);
        }

        /// <summary>
        /// Runs a task that returns a result, but will immediately throw if cancellation is requested.
        /// Note that the Task will continue to run in the background even if cancellation is requested, hence the "Unsafe" name.
        /// </summary>
        /// <exception cref="OperationCanceledException"></exception>
        /// <typeparam name="T"></typeparam>
        /// <param name="t"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public static async Task<T> RunWithUnsafeCancellationAsync<T>(this Task<T> t, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            TaskCompletionSource<object> source = new TaskCompletionSource<object>();
            using (token.Register(() => source.SetResult(null)))
            {
                await Task.WhenAny(t, source.Task);
                token.ThrowIfCancellationRequested();
                return t.Result;
            }
        }

        /// <summary>
        /// Schedules/returns another Task to run when, and only when, the given task runs to completion. Propagates exceptions.
        /// This overload is for async continuations that don't need the previous Task's Result.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static Task WhenCompleteContinueWith(this Task t, Func<Task> next)
        {
            return t.ContinueWith(p =>
            {
                if (p.Exception != null)
                {
                    throw p.Exception;
                }

                return next();
            }, TaskContinuationOptions.NotOnCanceled).Unwrap();
        }

        /// <summary>
        /// Schedules/returns another Task to run when, and only when, the given task runs to completion. Propagates exceptions.
        /// This overload is for async continuations that use the previous Task's Result.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static Task WhenCompleteContinueWith<T>(this Task<T> t, Func<T, Task> next)
        {
            return t.ContinueWith(p =>
            {
                if (p.Exception != null)
                {
                    throw p.Exception;
                }

                return next(p.Result);
            }, TaskContinuationOptions.NotOnCanceled).Unwrap();
        }

        /// <summary>
        /// Schedules/returns another Task to run when, and only when, the given task runs to completion. Propagates exceptions.
        /// This overload is for async continuations that don't need the previous Task's Result, but themselves return a result
        /// </summary>
        /// <param name="t"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static Task<T> WhenCompleteContinueWith<T>(this Task t, Func<Task<T>> next)
        {
            return t.ContinueWith(p =>
            {
                if (p.Exception != null)
                {
                    throw p.Exception;
                }

                return next();
            }, TaskContinuationOptions.NotOnCanceled).Unwrap();
        }

        /// <summary>
        /// Schedules/returns another Task to run when, and only when, the given task runs to completion. Propagates exceptions.
        /// This overload is for synchronous continuations that don't need the previous Task's Result.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static Task WhenCompleteContinueWith(this Task t, Action next)
        {
            return t.ContinueWith(p =>
            {
                if (p.Exception != null)
                {
                    throw p.Exception;
                }

                next();
            }, TaskContinuationOptions.NotOnCanceled);
        }

        /// <summary>
        /// Schedules/returns another Task to run when, and only when, the given task runs to completion. Propagates exceptions.
        /// This overload is for synchronous continuations that use the previous Task's Result.
        /// </summary>
        /// <param name="t"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        public static Task WhenCompleteContinueWith<T>(this Task<T> t, Action<T> next)
        {
            return t.ContinueWith(p =>
            {
                if (p.Exception != null)
                {
                    throw p.Exception;
                }

                next(p.Result);
            }, TaskContinuationOptions.NotOnCanceled);
        }
    }
}