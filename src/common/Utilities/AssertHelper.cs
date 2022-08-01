// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// Standard versions of methods throw InvalidOperationException. *Debug versions use Debug.Assert()
    /// </summary>
    internal static class AssertHelper
    {
        public static void NotNull(object value, string valueName)
        {
            _Assert(value != null, string.Format(CultureInfo.CurrentCulture, "{0} should not be null", valueName));
        }

        public static void NotNullDebug(object value, string valueName)
        {
            _DebugAssert(value != null, string.Format(CultureInfo.CurrentCulture, "{0} should not be null", valueName));
        }

        public static void Null(object value, string valueName)
        {
            _Assert(value == null, string.Format(CultureInfo.CurrentCulture, "{0} should be null", valueName));
        }

        public static void NullDebug(object value, string valueName)
        {
            _DebugAssert(value == null, string.Format(CultureInfo.CurrentCulture, "{0} should be null", valueName));
        }

        public static void False(bool value, string message = null)
        {
            _Assert(!value, message ?? "value should be false");
        }

        public static void FalseDebug(bool value, string message = null)
        {
            _DebugAssert(!value, message ?? "value should be false");
        }

        public static void True(bool value, string message = null)
        {
            _Assert(value, message ?? "value should be true");
        }

        public static void TrueDebug(bool value, string message = null)
        {
            _DebugAssert(value, message ?? "value should be true");
        }

        public static void NotNullOrEmpty(string value, string valueName)
        {
            _Assert(!string.IsNullOrEmpty(value), string.Format(CultureInfo.CurrentCulture, "{0} should not be null or empty", valueName));
        }

        public static void NotNullOrEmptyDebug(string value, string valueName)
        {
            _DebugAssert(!string.IsNullOrEmpty(value), string.Format(CultureInfo.CurrentCulture, "{0} should not be null or empty", valueName));
        }

        public static void NotEmpty(Guid guid)
        {
            _Assert(guid != Guid.Empty, "guid should not be empty!");
        }

        public static void NotEmptyDebug(Guid guid)
        {
            _DebugAssert(guid != Guid.Empty, "guid should not be empty!");
        }

        public static void NotEmpty<T>(IEnumerable<T> enumerable, string message = null)
        {
            _Assert(enumerable?.Any() ?? false, message ?? "collection should not be empty!");
        }

        public static void NotEmptyDebug<T>(IEnumerable<T> enumerable, string message = null)
        {
            _DebugAssert(enumerable?.Any() ?? false, message ?? "collection should not be empty!");
        }

        public static void Fail(string message = null)
        {
            _Assert(false, message ?? "failure triggered");
        }

        public static void FailDebug(string message = null)
        {
            Debug.Fail(message ?? "failure triggered");
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        private static void _DebugAssert(bool condition, string message)
        {
            Debug.Assert(condition, message);
            if (!condition && Debugger.IsAttached)
            {
                Debugger.Break();
            }
        }

        private static void _Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}