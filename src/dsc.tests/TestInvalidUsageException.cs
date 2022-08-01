// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Exceptions;

namespace Microsoft.BridgeToKubernetes.Exe.Tests
{
    public class TestInvalidUsageException : Exception, IUserVisibleExceptionReporter
    {
        public TestInvalidUsageException(string message)
            : base(message)
        { }

        public TestInvalidUsageException(string message, Exception inner)
            : base(message, inner)
        { }
    }
}