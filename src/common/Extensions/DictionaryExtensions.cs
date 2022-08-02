// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace System.Collections.Generic
{
    /// <summary>
    /// Extension methods for Dictionaries
    /// </summary>
    internal static class DictionaryExtensions
    {
        /// <summary>
        /// Gets a value, or calls a factory function to add it then returns it
        /// </summary>
        public static Y GetOrAdd<X, Y>(this IDictionary<X, Y> dict, X key, Func<Y> factory)
        {
            if (dict == null)
            {
                throw new NullReferenceException(nameof(dict));
            }

            if (!dict.ContainsKey(key))
            {
                dict[key] = factory();
            }

            return dict[key];
        }

        /// <summary>
        /// Returns true if the the current dictionary is a subset of the superset dicionary,
        /// meaning all the kv pairs of the current dictionary are contained in the superset dictionary.
        /// </summary>
        /// <typeparam name="X"></typeparam>
        /// <typeparam name="Y"></typeparam>
        /// <param name="subset">Current dictionary</param>
        /// <param name="superset">Dictionary to be tested to see if it contains the current dictionary</param>
        /// <returns></returns>
        public static bool IsSubsetOf<X, Y>(this IDictionary<X, Y> subset, IDictionary<X, Y> superset)
        {
            foreach (var kv in subset)
            {
                if (!superset.Contains(kv))
                {
                    return false;
                }
            }
            return true;
        }
    }
}