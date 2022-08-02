// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Exceptions;

namespace Microsoft.BridgeToKubernetes.Exe
{
    internal class CliValidationException : Exception, IUserVisibleExceptionReporter
    {
        public CliValidationException(string message) : base(message)
        { }

        public CliValidationException() : base()
        { }

        public CliValidationException(string message, Exception inner) : base(message, inner)
        { }
    }
}