// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s.Autorest;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System;

namespace Microsoft.BridgeToKubernetes.Library.Exceptions
{
    /// <summary>
    /// This is the base class for Exceptions that have Correlation Ids
    /// </summary>
    public class OperationIdException : PIIException, IOperationIds, IRequestResponse
    {
        private readonly IOperationContext _operationContext;

        internal OperationIdException(IOperationContext operationContext, string format, params object[] args)
            : base(format, args)
        {
            _operationContext = operationContext;
        }

        internal OperationIdException(IOperationContext operationContext, Exception innerException, string format, params object[] args)
            : base(innerException, format, args)
        {
            _operationContext = operationContext;
        }

        internal OperationIdException(IOperationContext operationContext, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, string format, params object[] args)
            : this(operationContext, format, args)
        {
            this.Request = request;
            this.Response = response;
        }

        internal OperationIdException(IOperationContext operationContext, HttpRequestMessageWrapper request, HttpResponseMessageWrapper response, Exception inner, string format, params object[] args)
            : this(operationContext, inner, format, args)
        {
            this.Request = request;
            this.Response = response;
        }

        public string RequestId => _operationContext?.RequestId;

        public string ClientRequestId => _operationContext?.ClientRequestId;

        public string CorrelationRequestId => _operationContext?.CorrelationId;

        public HttpRequestMessageWrapper Request { get; }

        public HttpResponseMessageWrapper Response { get; }

        internal override PIIException CloneWithFinalMessage(string message)
        {
            return new OperationIdException(_operationContext, Request, Response, message);
        }

        internal override PIIException CloneWithFinalMessage(string message, Exception innerEx)
        {
            return new OperationIdException(_operationContext, Request, Response, innerEx, message);
        }
    }
}