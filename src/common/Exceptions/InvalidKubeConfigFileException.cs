// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Exceptions
{
    internal class InvalidKubeConfigFileException : PIIException, IUserVisibleExceptionReporter
    {
        internal InvalidKubeConfigFileException(string format, params object[] args)
            : base(format, args)
        {
        }

        internal InvalidKubeConfigFileException(Exception innerException, string format, params object[] args)
            : base(innerException, format, args)
        {
        }

        internal override PIIException CloneWithFinalMessage(string message)
        {
            return new InvalidKubeConfigFileException(message);
        }

        internal override PIIException CloneWithFinalMessage(string message, Exception innerEx)
        {
            return new InvalidKubeConfigFileException(innerEx, message);
        }
    }
}