// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace System
{
    internal static class TimeSpanExtensions
    {
        public static string GetUIFormattedString(this TimeSpan elapsed, bool roundToLargestUnitOfTime = false)
        {
            if (elapsed.TotalSeconds < 1)
            {
                return "<1s";
            }
            else if (elapsed.TotalSeconds < 60)
            {
                return elapsed.ToString("%s's'");
            }
            else if (elapsed.TotalMinutes < 60)
            {
                return roundToLargestUnitOfTime ? elapsed.ToString("%m'm'") : elapsed.ToString("%m'm '%s's'");
            }
            else if (elapsed.TotalHours < 24)
            {
                return roundToLargestUnitOfTime ? elapsed.ToString("%h'h'") : elapsed.ToString("%h'h '%m'm'");
            }
            // times greater than 1 d
            return roundToLargestUnitOfTime ? elapsed.ToString("%d'd'") : elapsed.ToString("%d'd '%h'h'");
        }
    }
}