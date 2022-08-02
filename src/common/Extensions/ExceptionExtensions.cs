// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.ObjectModel;
using System.Reflection;

namespace System
{
    /// <summary>
    /// Extensions for Exceptions
    /// </summary>
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Gets the innermost exception
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static Exception GetInnermostException(this Exception e)
        {
            if (e == null || e.InnerException == null)
            {
                return e;
            }
            else
            {
                // Recurse
                return GetInnermostException(e.InnerException);
            }
        }

        public static void ReplaceInnerException(this Exception outerEx, Exception innerEx)
        {
            try
            {
                var innerExceptionProperty = typeof(Exception).GetField("_innerException", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField);
                innerExceptionProperty.SetValue(outerEx, innerEx);
            }
            catch (Exception) { }
        }

        public static void ReplaceInnerExceptions(this AggregateException outerEx, Exception[] innerExceptions)
        {
            try
            {
                var innerExceptionProperty = typeof(AggregateException).GetField("m_innerExceptions", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField);
                innerExceptionProperty.SetValue(outerEx, new ReadOnlyCollection<Exception>(innerExceptions));
            }
            catch (Exception) { }
        }

        public static void ReplaceStackTrace(this Exception ex, string stackTrace)
        {
            try
            {
                var stackTraceProperty = typeof(Exception).GetField("_stackTraceString", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.SetField);
                stackTraceProperty.SetValue(ex, stackTrace);
            }
            catch (Exception) { }
        }
    }
}