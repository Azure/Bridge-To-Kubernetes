// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace System
{
    internal static class TypeExtensions
    {
        /// <summary>
        /// Determines whether the given object is within the provided list
        /// </summary>
        public static bool IsIn<T>(this T obj, IEnumerable<T> list, IEqualityComparer<T> comparer = null)
        {
            if (!(list?.Any() ?? false))
            {
                return false;
            }

            if (comparer == null)
            {
                return list.Contains(obj);
            }
            else
            {
                return list.Contains(obj, comparer);
            }
        }

        /// <summary>
        /// Determines whether the given object is within the provided args
        /// </summary>
        public static bool IsIn<T>(this T obj, params T[] list)
        {
            return IsIn(obj, (IEnumerable<T>)list);
        }

        /// <summary>
        /// Determines whether the given object is within the provided args
        /// </summary>
        public static bool IsIn<T>(this T obj, IEqualityComparer<T> comparer = null, params T[] list)
        {
            return IsIn(obj, (IEnumerable<T>)list, comparer);
        }

        /// <summary>
        ///  Checks if a given type is one of ours, that is, not declared by system libraries or third parties.
        /// </summary>
        /// <param name="type">The type to be checked.</param>
        /// <returns>true if the type is one of our types, false otherwise.</returns>
        public static bool IsBridgeToKubernetesType(this Type type)
        {
            return type.Assembly.FullName.StartsWith("Microsoft.BridgeToKubernetes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a given value is the default value for a given type.
        /// </summary>
        /// <param name="type">Type to be checked.</param>
        /// <param name="value">Value to be checked.</param>
        /// <returns>True if the given value is the default for the type, false otherwise.</returns>
        public static bool IsDefaultValue(this Type type, object value)
        {
            if (type.IsValueType)
            {
                var defaultValue = Activator.CreateInstance(type);
                if (defaultValue == null)
                {
                    return value == null;
                }
                else
                {
                    return defaultValue.Equals(value);
                }
            }
            else
            {
                return value == null;
            }
        }
    }
}