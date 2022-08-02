// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Exceptions
{
    /// <summary>
    /// Exception from execution of a kubectl command
    /// </summary>
    public class KubectlException : PIIException, IUserVisibleExceptionReporter
    {
        internal KubectlException(string format, params object[] args)
            : base(format, args)
        {
        }

        internal KubectlException(Exception innerException, string format, params object[] args)
            : base(innerException, format, args)
        {
        }

        internal override PIIException CloneWithFinalMessage(string message)
        {
            return new KubectlException(message);
        }

        internal override PIIException CloneWithFinalMessage(string message, Exception innerEx)
        {
            return new KubectlException(innerEx, message);
        }
    }
}