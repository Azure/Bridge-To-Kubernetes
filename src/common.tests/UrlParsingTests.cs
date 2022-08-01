// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class UrlParsingTests
    {
        private HttpRequestMessage _httpRequest;

        public UrlParsingTests()
        {
            _httpRequest = new HttpRequestMessage();
        }

        [Fact]
        public void ValidateQueryParameter()
        {
            // Arrange
            var parameterName = "paramName";
            var parameterValue = "paramValue";
            this._httpRequest.RequestUri = new Uri($"https://bing.com/urlparsing?{parameterName}={parameterValue}");

            // Act
            var actual = UrlParsing.GetQueryParameter(this._httpRequest, parameterName);

            // Assert
            Assert.Equal(parameterValue, actual);
        }

        [Fact]
        public void ValidateEmptyQueryParameterValue()
        {
            // Arrange
            var parameterName = "paramName";
            this._httpRequest.RequestUri = new Uri($"https://bing.com/urlparsing?{parameterName}=");

            // Act
            var actual = UrlParsing.GetQueryParameter(this._httpRequest, parameterName);

            // Assert
            Assert.True(string.IsNullOrWhiteSpace(actual));
        }

        [Fact]
        public void ValidateNoQueryParameter()
        {
            // Arrange
            this._httpRequest.RequestUri = new Uri($"https://bing.com/urlparsing");

            // Act
            var actual = UrlParsing.GetQueryParameter(this._httpRequest, "randomParameter");

            // Assert
            Assert.True(string.IsNullOrWhiteSpace(actual));
        }

        [Fact]
        public void ValidateDifferentQueryParameter()
        {
            // Arrange
            this._httpRequest.RequestUri = new Uri("https://bing.com/urlparsing?parameterName=parameterValue");

            // Act
            var actual = UrlParsing.GetQueryParameter(this._httpRequest, "randomParameter");

            // Assert
            Assert.True(string.IsNullOrEmpty(actual));
        }

        [Fact]
        public void ValidateQueryParameterWithSpacesAndSymbols()
        {
            // Arrange
            var parameterName = "pane$*4";
            var parameterValue = "par ^7*tH";
            this._httpRequest.RequestUri = new Uri($"https://bing.com/urlparsing?{parameterName}={parameterValue}");

            // Act
            var actual = UrlParsing.GetQueryParameter(this._httpRequest, parameterName);

            // Assert
            Assert.Equal("par%20%5E7*tH", actual);
        }

        [Fact]
        public void ValidateQueryParameterWithSameNames()
        {
            // Arrange
            var parameterName = "sameParamName";
            var firstValue = "firstValue";
            var secondValue = "secondValue";
            this._httpRequest.RequestUri = new Uri($"https://bing.com/urlparsing?{parameterName}={firstValue}&{parameterName}={secondValue}");

            // Act
            var actual = UrlParsing.GetQueryParameter(this._httpRequest, parameterName);

            // Assert
            Assert.Equal(firstValue, actual);
        }
    }
}