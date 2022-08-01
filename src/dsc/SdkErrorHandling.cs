// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.Exceptions;

namespace Microsoft.BridgeToKubernetes.Exe
{
    /// <summary>
    /// Class for handling common SDK errors
    /// </summary>
    internal class SdkErrorHandling : ISdkErrorHandling
    {
        private readonly ILog _log;
        private readonly IConsoleOutput _out;

        public SdkErrorHandling(ILog log, IConsoleOutput consoleOut)
        {
            this._log = log;
            this._out = consoleOut;
        }

        /// <summary>
        /// <see cref="ISdkErrorHandling.TryHandleKnownException(Exception, string, bool)"/>
        /// </summary>
        public bool TryHandleKnownException(Exception e, string failedDependencyName, out string failureReason, bool displayUnkownErrors = false)
        {
            failureReason = TryHandleKnownExceptionAndGetMessage(e, failedDependencyName, displayUnkownErrors);
            return !string.IsNullOrWhiteSpace(failureReason);
        }

        /// <summary>
        /// <see cref="ISdkErrorHandling.TryHandleKnownExceptionAndGetMessage(Exception, string, bool)"/>
        /// </summary>
        /// <returns>The string message that was printed to console out, or null if not identified</returns>
        public string TryHandleKnownExceptionAndGetMessage(Exception e, string failedDependencyName, bool displayUnkownErrors = false)
        {
            // Any update to this logic should be reflected in SdkErrorHandlingTests.cs
            if (e is OperationCanceledException)
            {
                _log.Error(e.Message);
                var msg = Resources.Error_OperationCanceled;
                _out.Error(msg);
                return msg;
            }
            else if (e is IUserVisibleExceptionReporter)
            {
                _log.Warning(e.Message);
                _out.Error(e.Message);
                return e.Message;
            }

            // Don't recurse OperationIdExceptions
            if (e.InnerException != null && !(e is OperationIdException))
            {
                // Recurse
                return TryHandleKnownExceptionAndGetMessage(e.InnerException, failedDependencyName, displayUnkownErrors);
            }

            // If we get here, the "dependency" has failed.
            // Try to log the failed dependency
            if (!string.IsNullOrWhiteSpace(failedDependencyName))
            {
                string target = null;
                string requestId = null;
                string clientRequestId = null;
                string correlationRequestId = null;
                if (e is IRequestResponse rr)
                {
                    target = rr.Request?.RequestUri?.ToString();
                }
                if (e is IOperationIds op)
                {
                    requestId = op.RequestId;
                    clientRequestId = op.ClientRequestId;
                    correlationRequestId = op.CorrelationRequestId;
                }

                _log.Dependency(name: failedDependencyName,
                                target: target,
                                success: false,
                                properties: new Dictionary<string, object>
                                {
                                    { "RequestId", requestId },
                                    { "ClientRequestId", clientRequestId },
                                    { "CorrelationRequestId", correlationRequestId }
                                });
            }

            // Unkown error occurred
            if (displayUnkownErrors)
            {
                _log.Exception(e);
                _out.Error(e.Message);
                return e.Message;
            }

            return null;
        }
    }
}