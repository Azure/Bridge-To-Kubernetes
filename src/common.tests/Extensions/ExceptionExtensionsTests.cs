// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Extensions
{
    public class ExceptionExtensionsTests
    {
        [Fact]
        public void ValidateGetInnerExceptionWithNull()
        {
            // Arrange
            var exception = new Exception("Outer Exception", null);

            // Act
            var actual = exception.GetInnermostException();

            // Assert
            Assert.Equal(exception, actual);
        }

        [Fact]
        public void ValidateGetInnerExceptionWithOneLevel()
        {
            // Arrange
            var expected = new IndexOutOfRangeException("Inner exception");
            var exception = new Exception("Outer Exception", expected);

            // Act
            var actual = exception.GetInnermostException();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateGetInnerExceptionWithTwoLevel()
        {
            // Arrange
            var expected = new IndexOutOfRangeException("Inner most exception");
            var exception = new Exception("Outer Exception", new EntryPointNotFoundException("Middle level", expected));

            // Act
            var actual = exception.GetInnermostException();

            // Assert
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateReplaceStackTrace()
        {
            // Arrange
            var expected = "This is custom stack trace";
            var exception = new Exception("Stack will be replaced for this exception");

            // Act
            exception.ReplaceStackTrace(expected);

            // Assert
            var actual = exception.StackTrace;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateReplaceInnerException()
        {
            // Arrange
            var expected = new IndexOutOfRangeException("Will be added");
            var exception = new Exception("This exception's inner exception will be replaced", new ArgumentNullException("Will be replaced"));

            // Act
            exception.ReplaceInnerException(expected);

            // Assert
            var actual = exception.InnerException;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateReplaceInnerExceptionWithNoInnerException()
        {
            // Arrange
            var expected = new IndexOutOfRangeException("Will be added");
            var exception = new Exception("This exception's inner exception will be replaced");

            // Act
            exception.ReplaceInnerException(expected);

            // Assert
            var actual = exception.InnerException;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateReplaceInnerExceptionForAggregateExceptions()
        {
            // Arrange
            var expected = new IndexOutOfRangeException("Will be added");
            var exception = new AggregateException("This exception's inner exception will be replaced", new ArgumentNullException("Will be replaced"));

            // Act
            exception.ReplaceInnerException(expected);

            // Assert
            var actual = exception.InnerException;
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidateReplaceInnerExceptionForAggregateExceptionsWithNoInnerException()
        {
            // Arrange
            var expected = new IndexOutOfRangeException("Will be added");
            var exception = new AggregateException("This exception's inner exception will be replaced");

            // Act
            exception.ReplaceInnerException(expected);

            // Assert
            var actual = exception.InnerException;
            Assert.Equal(expected, actual);
        }
    }
}