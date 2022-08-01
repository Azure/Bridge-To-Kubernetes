// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Exceptions;

namespace Microsoft.BridgeToKubernetes.Library.Tests.Utils
{
    public class FakeInvalidUsageException : Exception, IUserVisibleExceptionReporter
    {
    }
}