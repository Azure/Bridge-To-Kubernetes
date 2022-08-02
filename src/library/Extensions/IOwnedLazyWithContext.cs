// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Library.Extensions
{
    internal interface IOwnedLazyWithContext<TOwned> : IDisposable
    {
        Lazy<TOwned> Value { get; }

        IOperationContext OperationContext { get; }

        ILog Log { get; }
    }

    internal interface IOwnedLazyWithContext<TArg1, TOwned> : IOwnedLazyWithContext<TOwned>
    {
    }

    internal interface IOwnedLazyWithContext<TArg1, TArg2, TOwned> : IOwnedLazyWithContext<TOwned>
    {
    }
}