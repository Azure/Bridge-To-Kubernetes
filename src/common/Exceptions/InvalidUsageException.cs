// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using Microsoft.BridgeToKubernetes.Common.Logging;
using k8s.Autorest;

namespace Microsoft.BridgeToKubernetes.Common.Exceptions
{
    /// <summary>
    /// This exception is thrown when the product was not used correctly.
    /// Examples include invalid inputs, missing configurations, etc.
    /// The provided message must by actionable to help the user fix the error.
    /// </summary>
    public class InvalidUsageException : PIIException, IOperationIds, IRequestResponse, IUserVisibleExceptionReporter
    {
        private readonly IOperationContext _operationContext;

        internal InvalidUsageException(IOperationContext context, string format, params object[] args)
            : base(format, args)
        {
            this._operationContext = context;
            if (this._operationContext != null)
            {
                this.RequestId = this._operationContext.RequestId;
                this.CorrelationRequestId = this._operationContext.CorrelationId;
                this.ClientRequestId = this._operationContext.ClientRequestId;
            }
        }

        internal InvalidUsageException(IOperationContext context, Exception inner, string format, params object[] args)
            : base(inner, format, args)
        {
            this._operationContext = context;
            if (this._operationContext != null)
            {
                this.RequestId = this._operationContext.RequestId;
                this.CorrelationRequestId = this._operationContext.CorrelationId;
                this.ClientRequestId = this._operationContext.ClientRequestId;
            }
        }

        internal InvalidUsageException(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, string format, params object[] args)
            : base(format, args)
        {
            this.Request = request;
            this.Response = response;
            this.RequestId = response?.GetRequestId();
            this.CorrelationRequestId = response?.GetCorrelationRequestId();
            this.ClientRequestId = request?.GetClientRequestId();
        }

        internal InvalidUsageException(HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, Exception innerException, string format, params object[] args)
            : base(innerException, format, args)
        {
            this.Request = request;
            this.Response = response;
            this.RequestId = response?.GetRequestId();
            this.CorrelationRequestId = response?.GetCorrelationRequestId();
            this.ClientRequestId = request?.GetClientRequestId();
        }

        internal InvalidUsageException(HttpRequestMessage request, HttpResponseMessage response, string format, params object[] args)
            : this(request.AsWrapper(), response.AsWrapper(), format, args)
        {
        }

        internal InvalidUsageException(HttpRequestMessage request, HttpResponseMessage response, Exception innerException, string format, params object[] args)
            : this(request.AsWrapper(), response.AsWrapper(), innerException, format, args)
        {
        }

        /// <summary>
        /// Http Request wrapper
        /// </summary>
        public HttpRequestMessageWrapper Request { get; }

        /// <summary>
        /// Http Response wrapper
        /// </summary>
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

        internal override PIIException CloneWithFinalMessage(string message)
        {
            if (this._operationContext != null)
            {
                return new InvalidUsageException(_operationContext, message);
            }
            else
            {
                return new InvalidUsageException(this.Request, this.Response, message);
            }
        }

        internal override PIIException CloneWithFinalMessage(string message, Exception innerEx)
        {
            if (this._operationContext != null)
            {
                return new InvalidUsageException(_operationContext, innerEx, message);
            }
            else
            {
                return new InvalidUsageException(this.Request, this.Response, innerEx, message);
            }
        }
    }
}