// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    public class RoutingException : PIIException
    {
        internal RoutingException(string format, params object[] args)
            : base(format, args)
        {
        }

        internal RoutingException(Exception innerException, string format, params object[] args)
            : base(innerException, format, args)
        {
        }

        internal override PIIException CloneWithFinalMessage(string message)
        {
            return new RoutingException(message);
        }

        internal override PIIException CloneWithFinalMessage(string message, Exception innerEx)
        {
            return new RoutingException(innerEx, message);
        }
    }
}