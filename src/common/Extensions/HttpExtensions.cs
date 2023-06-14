// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Json;
using System.Linq;

namespace System.Net.Http
{
    internal static class HttpExtensions
    {
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

        public static HttpRequestMessageWrapper AsWrapper(this HttpRequestMessage request)
        {
            string content = null;
            try
            {
                content = request?.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch { }

            return new HttpRequestMessageWrapper(request, content);
        }

        public static HttpResponseMessageWrapper AsWrapper(this HttpResponseMessage response)
        {
            string content = null;
            try
            {
                content = response?.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            catch { }

            return new HttpResponseMessageWrapper(response, content);
        }

        /// <summary>
        /// Throws if the <see cref="HttpResponseMessage.StatusCode"/> is not in the provided list of codes
        /// </summary>
        /// <exception cref="HttpRequestException"></exception>
        public static void EnsureStatusCodes(this HttpResponseMessage response, params HttpStatusCode[] codes)
        {
            if (codes == null || !codes.Any())
            {
                throw new ArgumentException($"'{nameof(codes)}' cannot be null or empty");
            }

            if (!response.StatusCode.IsIn(codes))
            {
                throw new HttpRequestException($"Response code '{(int)response.StatusCode}' is not in the expected list: {JsonHelpers.SerializeForLoggingPurpose(codes.Cast<int>())}");
            }
        }

        /// <summary>
        /// Throws if the <see cref="HttpResponseMessage.StatusCode"/> is not a success code or in the provided list of codes
        /// </summary>
        /// <exception cref="HttpRequestException"></exception>
        public static void EnsureSuccessStatusCodeOr(this HttpResponseMessage response, params HttpStatusCode[] codes)
        {
            if (codes == null || !codes.Any())
            {
                throw new ArgumentException($"'{nameof(codes)}' cannot be null or empty");
            }

            EnsureStatusCodes(response, SuccessCodes.Concat(codes).ToArray());
        }

        /// <summary>
        /// Returns whether the status code equals Created or OK
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static bool IsSuccessStatusCode(this HttpStatusCode code)
        {
            return code.IsIn(SuccessCodes);
        }

        /// <summary>
        /// Returns whether the status code is "Successful", or 400-level (not our fault)
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        public static bool IsNonErrorStatusCode(this HttpStatusCode code)
        {
            bool returnValue = IsSuccessStatusCode(code);
            if (!returnValue)
            {
                returnValue = code.IsIn(NonErrorCodes);
            }

            return returnValue;
        }

        /// <summary>
        /// Adds common Microsoft request ids to the default request headers
        /// </summary>
        /// <param name="client"></param>
        /// <param name="context"></param>
        public static void AddMsRequestHeaders(this HttpClient client, IOperationContext context)
        {
            client.DefaultRequestHeaders.Add(Constants.CustomHeaderNames.ClientRequestId, context.ClientRequestId);
        }
    }
}