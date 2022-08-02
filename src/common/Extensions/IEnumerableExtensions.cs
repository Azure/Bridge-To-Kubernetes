// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace System.Collections.Generic
{
    internal static class IEnumerableExtensions
    {
        private static Random _random = new Random();

        public static void ExecuteForEach<T>(this IEnumerable<T> items, Action<T> action, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (items != null)
            {
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    action(item);
                }
            }
        }

        private static async Task ExecuteForEachInParallelAsync<T>(this IEnumerable<T> items, Action<T> action, CancellationToken cancellationToken, int maxParallelThreads = 5)
        {
            CancellationTokenSource innerSource = new CancellationTokenSource();
            using (SemaphoreSlim maxParallelTasks = new SemaphoreSlim(maxParallelThreads))
            {
                List<Task> tasks = new List<Task>();
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        await maxParallelTasks.WaitAsync();
                        var task = Task.Run(() =>
                        {
                            action(item);
                        }, innerSource.Token).ContinueWith(t => maxParallelTasks.Release());
                        tasks.Add(task);
                    }
                }

                await Task.WhenAll(tasks);
            }
        }

        public static Task ExecuteForEachAsync<T>(this IEnumerable<T> items, Func<T, Task> asyncFunc)
        {
            return items != null ? Task.WhenAll(items.Select(x => asyncFunc(x))) : Task.CompletedTask;
        }

        public static Task<R[]> ExecuteForEachAsync<T, R>(this IEnumerable<T> items, Func<T, Task<R>> asyncFunc)
        {
            return items != null ? Task.WhenAll(items.Select(x => asyncFunc(x))) : new Task<R[]>(() => new R[0]);
        }

        public static IEnumerable<T> WaitAll<T>(this IEnumerable<Task<T>> tasks)
        {
            return AsyncHelpers.RunSync(() => Task.WhenAll(tasks));
        }

        public static void WaitAll(this IEnumerable<Task> tasks)
        {
            AsyncHelpers.RunSync(() => Task.WhenAll(tasks));
        }

        public static async Task<IEnumerable<T>> WaitAllAsync<T>(this IEnumerable<Task<T>> tasks)
        {
            return await Task.WhenAll(tasks);
        }

        public static Task WaitAllAsync(this IEnumerable<Task> tasks)
        {
            return Task.WhenAll(tasks);
        }

        /// <summary>
        /// Use the Fisher-Yates algorithm to shuffle the items and return a new IEnumerable.
        /// </summary>
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> items)
        {
            var list = items.ToList();
            var n = list.Count;
            for (int i = 0; i < n; i++)
            {
                var j = i + _random.Next(n - i);
                var temp = list[j];
                list[j] = list[i];
                list[i] = temp;
            }
            return list;
        }

        /// <summary>
        /// Returns an enumerable that excludes elements from the second enumerable where the key selectors produce a match
        /// </summary>
        public static IEnumerable<X> Except<X, Y, Z>(this IEnumerable<X> source, IEnumerable<Y> excludes, Func<X, Z> sourceSelector, Func<Y, Z> excludeSelector, IEqualityComparer<Z> equalityComparer = null)
        {
            ILookup<Z, Y> excludesLookup;
            if (equalityComparer == null)
            {
                excludesLookup = excludes.ToLookup(e => excludeSelector(e));
            }
            else
            {
                excludesLookup = excludes.ToLookup(e => excludeSelector(e), equalityComparer);
            }

            return source.Where(s => !excludesLookup.Contains(sourceSelector(s)));
        }

        /// <summary>
        /// Returns an enumerable that excludes elements from the second enumerable where the key selectors produce a match
        /// </summary>
        public static IEnumerable<X> Except<X, Y>(this IEnumerable<X> source, IEnumerable<Y> excludes, Func<Y, X> excludeSelector, IEqualityComparer<X> equalityComparer = null)
        {
            return Except(source, excludes, s => s, excludeSelector, equalityComparer);
        }

        /// <summary>
        /// Returns an enumerable that excludes elements from the second enumerable where the key selectors produce a match
        /// </summary>
        public static IEnumerable<X> Except<X, Y>(this IEnumerable<X> source, IEnumerable<Y> excludes, Func<X, Y> sourceSelector, IEqualityComparer<Y> equalityComparer = null)
        {
            return Except(source, excludes, sourceSelector, e => e, equalityComparer);
        }

        /// <summary>
        /// Produces the set intersection of two sequences.
        /// </summary>
        public static IEnumerable<X> Intersect<X, Y>(this IEnumerable<X> first, IEnumerable<Y> second, Func<X, Y> sourceSelector)
        {
            return first.Where(s => second.Contains(sourceSelector(s)));
        }

        public static IEnumerable<T> AsEnumerable<T>(this T item)
        {
            yield return item;
        }
    }
}