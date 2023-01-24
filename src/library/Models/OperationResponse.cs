// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System.Net.Http;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    public class OperationResponse : IOperationIds
    {
        public HttpRequestMessageWrapper Request { get; }

        public HttpResponseMessageWrapper Response { get; }

        /// <summary>
        /// The x-ms-request-id header of the response received
        /// </summary>
        public string RequestId { get; }

        /// <summary>
        /// The x-ms-client-request-id header of the response received
        /// </summary>
        public string ClientRequestId { get; }

        /// <summary>
        /// The x-ms-correlation-request-id header of the response received
        /// </summary>
        public string CorrelationRequestId { get; }

        /// <summary>
        /// Constructor with an HttpRequestMessage and HttpResponseMessage
        /// </summary>
        /// <param name="request">The request that generated the response</param>
        /// <param name="response">The response received</param>
        internal OperationResponse(HttpRequestMessage request, HttpResponseMessage response)
            : this(request.AsWrapper(), response.AsWrapper())
        {
        }

        internal OperationResponse(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response)
        {
            this.Request = request;
            this.Response = response;
            this.RequestId = response?.GetRequestId();
            this.ClientRequestId = request?.GetClientRequestId();
            this.CorrelationRequestId = response?.GetCorrelationRequestId();
        }

        /// <summary>
        /// Constructor with an IOperationContext
        /// </summary>
        /// <param name="operationContext"></param>
        internal OperationResponse(IOperationContext operationContext)
        {
            this.RequestId = operationContext?.RequestId;
            this.ClientRequestId = operationContext?.ClientRequestId;
            this.CorrelationRequestId = operationContext?.CorrelationId;
        }
    }

    public class OperationResponse<T> : OperationResponse
    {
        public T Value { get; }

        public OperationResponse(T value, IOperationContext operationContext) : base(operationContext)
        {
            this.Value = value;
        }

        public OperationResponse(T value, HttpRequestMessage request, HttpResponseMessage response) : base(request, response)
        {
            this.Value = value;
        }

        public OperationResponse(T value, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response) : base(request, response)
        {
            this.Value = value;
        }
    }
}