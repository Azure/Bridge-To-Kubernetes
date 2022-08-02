// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Exceptions;

namespace Microsoft.BridgeToKubernetes.Library.Utilities
{
    internal static class AutofacUtilities
    {
        /// <summary>
        /// Runs a function that returns type T with common Autofac catch handlers.
        /// Propagates <see cref="IUserVisibleExceptionReporter"/> exceptions, but otherwise logs and throws <see cref="ManagementFactoryException"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static T TryRunWithErrorPropagation<T>(Func<T> func, ILog log, IOperationContext operationContext)
        {
            return AsyncHelpers.RunSync(() =>
            {
                return TryRunWithErrorPropagationAsync(() =>
                {
                    return Task.FromResult(func());
                }, log, operationContext);
            });
        }

        /// <summary>
        /// Runs an Action with common Autofac catch handlers.
        /// Propagates <see cref="IUserVisibleExceptionReporter"/> exceptions, but otherwise logs and throws <see cref="ManagementFactoryException"/>
        /// </summary>
        /// <param name="action"></param>
        /// <param name="log"></param>
        public static void TryRunWithErrorPropagation(Action action, ILog log, IOperationContext operationContext)
        {
            TryRunWithErrorPropagation(() =>
            {
                action();
                return (object)null;
            }, log, operationContext);
        }

        /// <summary>
        /// Runs a Task with common Autofac catch handlers.
        /// Propagates <see cref="IUserVisibleExceptionReporter"/> exceptions, but otherwise logs and throws <see cref="ManagementFactoryException"/>
        /// </summary>
        /// <param name="func"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static Task TryRunWithErrorPropagationAsync(Func<Task> func, ILog log, IOperationContext operationContext)
        {
            return TryRunWithErrorPropagationAsync(async () =>
            {
                await func();
                return (object)null;
            }, log, operationContext);
        }

        /// <summary>
        /// Runs a Task that returns a result with common Autfac catch handlers.
        /// Propagates <see cref="IUserVisibleExceptionReporter"/> exceptions, but otherwise logs and throws <see cref="ManagementFactoryException"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public static async Task<T> TryRunWithErrorPropagationAsync<T>(Func<Task<T>> func, ILog log, IOperationContext operationContext)
        {
            try
            {
                return await func();
            }
            catch (Exception e)
            {
                _Handle<T>(e, log, operationContext);
                // We should never get here, _Handle should always throw
                log.Critical($"Logic in {nameof(AutofacUtilities)}.{nameof(_Handle)}() did not produce an exception as expected");
                throw;
            }
        }

        private static void _Handle<T>(Exception e, ILog log, IOperationContext operationContext, bool isRecurse = false)
        {
            if (e is IUserVisibleExceptionReporter)
            {
                log.ExceptionAsWarning(e);
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            else if (e is ArgumentException)
            {
                log.Exception(e);
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            else if (e is OperationCanceledException)
            {
                log.ExceptionAsWarning(e);
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            else if (e is DependencyResolutionException && e.InnerException is IUserVisibleExceptionReporter)
            {
                log.ExceptionAsWarning(e);
                ExceptionDispatchInfo.Capture(e.InnerException).Throw();
            }
            else if (e is DependencyResolutionException)
            {
                if (e.InnerException is AggregateException)
                {
                    // Try to recurse
                    _Handle<T>(e.InnerException, log, operationContext, isRecurse: true);
                }

                // If we get here, we couldn't unwrap to a more specific exception
                try
                {
                    log.Exception(e);
                    log.Flush(TimeSpan.FromMilliseconds(1500));
                }
                catch { }

                throw new ManagementFactoryException(typeof(T), e, operationContext);
            }
            else if (e is OperationIdException)
            {
                // This exception has already been logged in the exception strategy class.
                ExceptionDispatchInfo.Capture(e).Throw();
            }
            else
            {
                if (e is AggregateException agg)
                {
                    log.Verbose($"Attempting to translate AggregateException with {agg.InnerExceptions.Count} exceptions");
                    log.ExceptionAsWarning(agg);
                    for (int i = 0; i < agg.InnerExceptions.Count; i++)
                    {
                        var ex = agg.InnerExceptions[i];
                        log.Verbose($"{i + 1}/{agg.InnerExceptions.Count}: {ex.GetType().Name}");
                        // Recurse
                        _Handle<T>(ex, log, operationContext, isRecurse: true);
                    }
                }

                if (!isRecurse)
                {
                    // If we get here, this is the root of any possible recursion tree and we were unable to unwrap to a more specific exception.
                    // Log and throw OperationIdException.
                    try
                    {
                        log.Exception(e);
                        log.Flush(TimeSpan.FromMilliseconds(1500));
                    }
                    catch { }

                    throw new OperationIdException(operationContext, e, e.Message);
                }
            }
        }
    }
}