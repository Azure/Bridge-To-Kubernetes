// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Globalization;

namespace System
{
    /// <summary>
    /// Extension methods for DateTime
    /// </summary>
    internal static class DateTimeExtensions
    {
        /// <summary>
        /// Converts a UTC date time to the smallest time unit relative to the current system's local date time.
        /// </summary>
        /// <param name="dateTimeToConvert">A datetime, assumed to be UTC unless explicitly specified.</param>
        /// <param name="roundToLargestUnitOfTime">Round the string to the largest unit of time (e.g. '4h' instead of '4h 37m'</param>
        /// <returns>A string representation of the relative time difference suffixed with 'ago'</returns>
        public static string FormatDateTimeUtcAsAgoString(this DateTime dateTimeToConvert, bool roundToLargestUnitOfTime = false)
        {
            if (dateTimeToConvert.ToUniversalTime() > DateTime.UtcNow)
            {
                throw new ArgumentException("Cannot convert a future date time.");
            }

            return dateTimeToConvert != default(DateTime) ? $"{(DateTime.UtcNow - dateTimeToConvert).GetUIFormattedString(roundToLargestUnitOfTime)} ago" : "-";
        }

        /// <summary>
        /// Changes DateTime to string in Invariant Culture
        /// </summary>
        /// <returns></returns>
        public static string ToDocDBString(this DateTime dateTime)
        {
            return dateTime.ToString(CultureInfo.InvariantCulture);
        }
    }
}