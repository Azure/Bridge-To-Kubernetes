// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common;

namespace Microsoft.BridgeToKubernetes.Library.Exceptions
{
    /// <summary>
    /// Thrown when the management factory cannot construct the requested type
    /// </summary>
    public sealed class ManagementFactoryException : OperationIdException
    {
        internal ManagementFactoryException(Type t, Exception ex, IOperationContext operationContext)
            : base(operationContext, ex, "Couldn't create {0}. Please contact support.", t.Name)
        { }
    }
}