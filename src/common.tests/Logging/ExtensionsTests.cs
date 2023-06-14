// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests.Logging
{
    public class ExtensionsTests
    {
        private readonly Dictionary<string, IEnumerable<string>> _headersToScramble = new Dictionary<string, IEnumerable<string>>()
            {
                { "user-Agent", new List<string>() { "azure-resource-manager/2.0" } },
                { "accept-Language", new List<string>() { "en-US" } },
                { "x-ms-client-request-id", new List<string>() { "101a3b44-8e64-430d-9e22-acf7048fd612" } },
                { "x-ms-client-principal-name", new List<string>() { "thisisauseremail@pii.com" } },
                { "x-ARR-ClientCert", new List<string>() { "a client cert" } },
                { "Authorization", new List<string>() { "authorization" } },
            };

        [Fact]
        public void GetScrambledHeaders()
        {
            var newHeaders = _headersToScramble.ScrambleAndAddPIIMarkersToHeaders();

            Assert.Equal("azure-resource-manager/2.0", newHeaders["user-Agent"].FirstOrDefault());
            Assert.Equal("en-US", newHeaders["accept-Language"].FirstOrDefault());
            Assert.Equal("101a3b44-8e64-430d-9e22-acf7048fd612", newHeaders["x-ms-client-request-id"].FirstOrDefault());
            Assert.Equal("thisisauseremail@pii.com", newHeaders["x-ms-client-principal-name"].FirstOrDefault());
            Assert.Equal("ab37455923f5834e", newHeaders["x-ARR-ClientCert"].FirstOrDefault());
            Assert.Equal("cbb82ebec6051b78", newHeaders["Authorization"].FirstOrDefault());
        }

        [Fact]
        public void GetScrambledHeadersWithSpecialScramble()
        {
            var newHeaders = _headersToScramble.ScrambleAndAddPIIMarkersToHeaders(addPIIMarkersDelegate: (value) => GetValueWithPIIMarkers(value));

            Assert.Equal("azure-resource-manager/2.0", newHeaders["user-Agent"].FirstOrDefault());
            Assert.Equal("en-US", newHeaders["accept-Language"].FirstOrDefault());
            Assert.Equal("<pii>101a3b44-8e64-430d-9e22-acf7048fd612</pii>4225a487e2a6126a", newHeaders["x-ms-client-request-id"].FirstOrDefault());
            Assert.Equal("<pii>thisisauseremail@pii.com</pii>03222cc8b3090319", newHeaders["x-ms-client-principal-name"].FirstOrDefault());
            Assert.Equal("ab37455923f5834e", newHeaders["x-ARR-ClientCert"].FirstOrDefault());
            Assert.Equal("cbb82ebec6051b78", newHeaders["Authorization"].FirstOrDefault());
        }

        private string GetValueWithPIIMarkers(PII piiValue)
        {
            // Geneva config is set up such that text between startMarker and endMarker is removed.
            // Logging our custom hash value so that corelation with client logs is possible
            return $"<pii>{piiValue.Value}</pii>{piiValue.ScrambledValue}";
        }
    }
}