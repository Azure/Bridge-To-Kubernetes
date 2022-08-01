// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class HttpExtensionsTests
    {
        #region Test data

        private static readonly HttpStatusCode[] AllCodes = Enum.GetValues(typeof(HttpStatusCode)).OfType<HttpStatusCode>().ToArray();

        private static readonly HttpStatusCode[] SuccessCodes = new HttpStatusCode[]
        {
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.Accepted,
            HttpStatusCode.NoContent
        };

        private static readonly HttpStatusCode[] NonErrorCodes = new HttpStatusCode[]
        {
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.NotFound,
            HttpStatusCode.Forbidden,
            HttpStatusCode.Conflict
        };

        private static readonly HttpStatusCode[] AllCodesExceptSuccess = AllCodes.Except(SuccessCodes).ToArray();

        public static IEnumerable<object[]> SuccessCodesData
            => SuccessCodes.AsTestData();

        public static IEnumerable<object[]> AllCodesExceptSuccessData
            => AllCodesExceptSuccess.AsTestData();

        public static IEnumerable<object[]> NonErrorCodesData
            => NonErrorCodes.AsTestData();

        #endregion Test data

        #region IsSuccessStatusCode

        [Theory]
        [MemberData(nameof(SuccessCodesData))]
        public void IsSuccessStatusCode_Positive(HttpStatusCode code)
        {
            Assert.True(code.IsSuccessStatusCode());
        }

        [Theory]
        [MemberData(nameof(AllCodesExceptSuccessData))]
        public void IsSuccessStatusCode_Negative(HttpStatusCode code)
        {
            Assert.False(code.IsSuccessStatusCode());
        }

        #endregion IsSuccessStatusCode

        #region IsNonErrorStatusCode

        [Theory]
        [MemberData(nameof(SuccessCodesData))]
        [MemberData(nameof(NonErrorCodesData))]
        public void IsNonErrorStatusCode_Positive(HttpStatusCode code)
        {
            Assert.True(code.IsNonErrorStatusCode());
        }

        public static IEnumerable<object[]> IsNonErrorStatusCode_Negative_Data
            => AllCodesExceptSuccess.Except(NonErrorCodes).AsTestData();

        [Theory]
        [MemberData(nameof(IsNonErrorStatusCode_Negative_Data))]
        public void IsNonErrorStatusCode_Negative(HttpStatusCode code)
        {
            Assert.False(code.IsNonErrorStatusCode());
        }

        #endregion IsNonErrorStatusCode

        #region EnsureStatusCodes

        [Fact]
        public void EnsureStatusCodes_Positive()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.InternalServerError))
            {
                response.EnsureStatusCodes(HttpStatusCode.InternalServerError);
                response.EnsureStatusCodes(HttpStatusCode.InternalServerError, HttpStatusCode.InsufficientStorage);
            }
        }

        [Fact]
        public void EnsureStatusCodes_ArgumentException()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.Gone))
            {
                Assert.Throws<ArgumentException>(() => response.EnsureStatusCodes());
            }
        }

        [Fact]
        public void EnsureStatusCodes_Negative()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.NotExtended))
            {
                Assert.Throws<HttpRequestException>(() => response.EnsureStatusCodes(HttpStatusCode.OK));
            }
        }

        #endregion EnsureStatusCodes

        #region EnsureSuccessStatusCodeOr

        [Theory]
        [MemberData(nameof(SuccessCodesData))]
        [InlineData(HttpStatusCode.NotAcceptable)]
        public void EnsureSuccessStatusCodeOr_Positive(HttpStatusCode code)
        {
            using (var response = new HttpResponseMessage(code))
            {
                response.EnsureSuccessStatusCodeOr(HttpStatusCode.NotAcceptable);
                response.EnsureSuccessStatusCodeOr(HttpStatusCode.NotAcceptable, HttpStatusCode.MultipleChoices);
            }
        }

        [Fact]
        public void EnsureSuccessStatusCodeOr_ArgumentException()
        {
            using (var response = new HttpResponseMessage(HttpStatusCode.NotImplemented))
            {
                Assert.Throws<ArgumentException>(() => response.EnsureSuccessStatusCodeOr());
            }
        }

        public static IEnumerable<object[]> EnsureSuccessStatusCodeOr_Negative_Data
            => AllCodesExceptSuccess.Except(new[] { HttpStatusCode.InternalServerError }).AsTestData();

        [Theory]
        [MemberData(nameof(EnsureSuccessStatusCodeOr_Negative_Data))]
        public void EnsureSuccessStatusCodeOr_Negative(HttpStatusCode code)
        {
            using (var response = new HttpResponseMessage(code))
            {
                Assert.Throws<HttpRequestException>(() => response.EnsureSuccessStatusCodeOr(HttpStatusCode.InternalServerError));
            }
        }

        #endregion EnsureSuccessStatusCodeOr

        #region AsWrapper

        [Fact]
        public void AsWrapper_Request()
        {
            Uri uri = new Uri("http://localhost:1234/foobar");

            // With content
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri) { Content = new StringContent("foo") })
            {
                var wrapper = request.AsWrapper();
                Assert.Equal(uri, wrapper.RequestUri);
                Assert.Equal(HttpMethod.Get, wrapper.Method);
                Assert.Equal("foo", wrapper.Content);
            }

            // Without content
            using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
            {
                var wrapper = request.AsWrapper();
                Assert.Equal(uri, wrapper.RequestUri);
                Assert.Equal(HttpMethod.Get, wrapper.Method);
                Assert.Null(wrapper.Content);
            }
        }

        [Fact]
        public void AsWrapper_Response()
        {
            // With content
            using (var response = new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("foo"), ReasonPhrase = "reasons" })
            {
                var wrapper = response.AsWrapper();
                Assert.Equal(response.ReasonPhrase, wrapper.ReasonPhrase);
                Assert.Equal(response.StatusCode, wrapper.StatusCode);
                Assert.Equal("foo", wrapper.Content);
            }

            // Without content
            using (var response = new HttpResponseMessage(HttpStatusCode.Accepted) { ReasonPhrase = "reasons" })
            {
                var wrapper = response.AsWrapper();
                Assert.Equal(response.ReasonPhrase, wrapper.ReasonPhrase);
                Assert.Equal(response.StatusCode, wrapper.StatusCode);
                Assert.Null(wrapper.Content);
            }
        }

        #endregion AsWrapper
    }
}