// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Library.Exceptions
{
    /// <summary>
    /// Exception thrown for validation errors
    /// </summary>
    public sealed class ValidationException : PIIException, IOperationIds, IUserVisibleExceptionReporter
    {
        private readonly IOperationContext _operationContext;

        internal ValidationException(IOperationContext operationContext, string format, params object[] args)
            : base(format, args)
        {
            if (operationContext != null)
            {
                RequestId = operationContext.RequestId;
                ClientRequestId = operationContext.ClientRequestId;
                CorrelationRequestId = operationContext.CorrelationId;
            }

            _operationContext = operationContext;
        }

        internal ValidationException(IOperationContext operationContext, Exception inner, string format, params object[] args)
            : base(inner, format, args)
        {
            if (operationContext != null)
            {
                RequestId = operationContext.RequestId;
                ClientRequestId = operationContext.ClientRequestId;
                CorrelationRequestId = operationContext.CorrelationId;
            }

            _operationContext = operationContext;
        }

        public string RequestId { get; }

        public string ClientRequestId { get; }

        public string CorrelationRequestId { get; }

        internal override PIIException CloneWithFinalMessage(string message)
        {
            return new ValidationException(_operationContext, message);
        }

        internal override PIIException CloneWithFinalMessage(string message, Exception innerEx)
        {
            return new ValidationException(_operationContext, innerEx, message);
        }
    }
}