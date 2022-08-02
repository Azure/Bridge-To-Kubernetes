// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Autofac;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Library.Utilities;

namespace Microsoft.BridgeToKubernetes.Library.Extensions
{
    internal static class OwnedExtensions
    {
        /// <summary>
        /// Prevents <see cref="DependencyResolutionException"/>s from propagating back up to users when using Owned<>
        /// </summary>
        /// <param name="operation"></param>
        public static X TryRunOwnedOperationThenDispose<T, X>(this IOwnedLazyWithContext<T> owned, Func<T, X> operation)
        {
            if (typeof(X).IsAssignableTo<Task>())
            {
                throw new InvalidOperationException($"Please use the Async version of this method");
            }

            return AutofacUtilities.TryRunWithErrorPropagation<X>(() =>
            {
                using (owned)
                {
                    return operation(owned.Value.Value);
                }
            }, owned.Log, owned.OperationContext);
        }

        /// <summary>
        /// Prevents <see cref="DependencyResolutionException"/>s from propagating back up to users when using Owned<>
        /// </summary>
        /// <param name="operation"></param>
        public static void TryRunOwnedOperationThenDispose<T>(this IOwnedLazyWithContext<T> owned, Action<T> operation)
        {
            AutofacUtilities.TryRunWithErrorPropagation(() =>
            {
                using (owned)
                {
                    operation(owned.Value.Value);
                }
            }, owned.Log, owned.OperationContext);
        }

        /// <summary>
        /// Prevents <see cref="DependencyResolutionException"/>s from propagating back up to users when using Owned<>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <returns></returns>
        public static Task<X> TryRunOwnedOperationThenDisposeAsync<T, X>(this IOwnedLazyWithContext<T> owned, Func<T, Task<X>> operation)
        {
            return AutofacUtilities.TryRunWithErrorPropagationAsync(async () =>
               {
                   using (owned)
                   {
                       return await operation(owned.Value.Value);
                   }
               },
               owned.Log, owned.OperationContext);
        }

        /// <summary>
        /// Prevents <see cref="DependencyResolutionException"/>s from propagating back up to users when using Owned<>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="operation"></param>
        /// <returns></returns>
        public static Task TryRunOwnedOperationThenDisposeAsync<T>(this IOwnedLazyWithContext<T> owned, Func<T, Task> operation)
        {
            return AutofacUtilities.TryRunWithErrorPropagationAsync(async () =>
            {
                using (owned)
                {
                    await operation(owned.Value.Value);
                }
            }, owned.Log, owned.OperationContext);
        }
    }
}