// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    // TODO remove?
    internal static class UrlParsing
    {
        public static string GetQueryParameter(HttpRequestMessage request, string parameterName)
        {
            AssertHelper.NotNull(request, nameof(request));
            AssertHelper.NotNull(parameterName, nameof(parameterName));
            return request.RequestUri.Query.TrimStart('?')
                .Split('&')
                .Select(x => x.Split('='))
                .Select(x => new KeyValuePair<string, string>(x[0], x[x.Length - 1]))
                .Where(pair => parameterName.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Value)
                .FirstOrDefault();
        }

        public static bool IsEndOfHttpResponseStream(StreamReader reader)
        {
            // When stream data from HTTP server, sometime when the stream ends, calling EndOfStream
            // throw IOException with the inner System.Net.Http.CurlException has 'Transferred a partial file'
            // message although the operation does completes. Add try/catch to handle this.
            try
            {
                return reader.EndOfStream;
            }
            catch (IOException ex) when (ex.Message.Contains("Unable to read data from the transport connection: The connection was closed.")
                                        || (ex.InnerException != null && ex.InnerException.Message.Contains("Transferred a partial file")))
            {
                return true;
            }
        }
    }
}