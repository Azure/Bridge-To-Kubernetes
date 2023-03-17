// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Globalization;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Extensions
{
    public class DateTimeExtensionsTests
    {
        [Fact]
        public void ValidateDateTimeFormatForHoursAndMins()
        {
            Assert.Equal("1h 4m ago", DateTime.UtcNow.AddHours(-1).AddMinutes(-4).FormatDateTimeUtcAsAgoString());
            Assert.Equal("1h ago", DateTime.UtcNow.AddHours(-1).AddMinutes(-4).FormatDateTimeUtcAsAgoString(true));
        }

        [Fact]
        public void ValidateDateTimeFormatForMinutes()
        {
            Assert.Equal("6m 0s ago", DateTime.UtcNow.AddMinutes(-6).FormatDateTimeUtcAsAgoString());
            Assert.Equal("6m ago", DateTime.UtcNow.AddMinutes(-6).FormatDateTimeUtcAsAgoString(true));
        }

        //Skiping for day light saving
        [Fact]
        public void ValidateDateTimeFormatForDays()
        {
            // For Daylight Savings Time
            var dateTimeNow = DateTime.Now;
            var dateTime7Days2HoursAgo = dateTimeNow.AddDays(-7).AddHours(-2);

            var difference = dateTime7Days2HoursAgo - dateTimeNow;
            Assert.Equal("7d 2h ago", DateTime.UtcNow.AddDays(difference.Days).AddHours(difference.Hours).FormatDateTimeUtcAsAgoString());
            Assert.Equal("7d ago", DateTime.UtcNow.AddDays(difference.Days).AddHours(difference.Hours).FormatDateTimeUtcAsAgoString(true));
        }

        [Fact]
        public void ValidateDateTimeForDifferentCulture()
        {
            CultureInfo.CurrentCulture = CultureInfo.CreateSpecificCulture("en-GB");
            Assert.Equal("2d 2h ago", DateTime.UtcNow.AddDays(-2).AddHours(-2).FormatDateTimeUtcAsAgoString());
            Assert.Equal("2d ago", DateTime.UtcNow.AddDays(-2).AddHours(-2).FormatDateTimeUtcAsAgoString(true));
        }

        [Fact]
        public void ValidateDateTimeFormatForInvalidString()
        {
            var dateTimeString = "invalid string";
            Assert.Throws<FormatException>(() => DateTimeExtensions.FormatDateTimeUtcAsAgoString(DateTime.Parse((dateTimeString))));
        }

        [Fact]
        public void ValidateDateTimeFormatForFutureDateTime()
        {
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.AddDays(4).FormatDateTimeUtcAsAgoString());
        }

        [Fact]
        public void ValidateDateTimeFormatForDefaultDateTimeString()
        {
            var dateTimeString = default(DateTime).ToUniversalTime().ToString("s") + "Z";
            Assert.Equal("-", DateTimeExtensions.FormatDateTimeUtcAsAgoString(DateTime.Parse(dateTimeString).ToUniversalTime()));
        }
    }
}