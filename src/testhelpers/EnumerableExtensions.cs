// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;

namespace System.Collections.Generic
{
    public static class EnumerableExtensions
    {
        /// <summary>
        /// Converts the enumerable to Xunit test data
        /// </summary>
        public static IEnumerable<object[]> AsTestData<T>(this IEnumerable<T> enumerable)
        {
            return enumerable.Select(x => new object[] { x });
        }
    }
}