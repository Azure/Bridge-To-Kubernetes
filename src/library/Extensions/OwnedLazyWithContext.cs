// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Library.Extensions
{
    internal class OwnedLazyWithContextBase<T>
    {
        protected ILifetimeScope myScope;

        public OwnedLazyWithContextBase(ILifetimeScope scope)
        {
            this.myScope = scope.BeginLifetimeScope();
            this.OperationContext = this.myScope.Resolve<IOperationContext>();
            this.Log = this.myScope.Resolve<ILog>();
        }

        public Lazy<T> Value { get; protected set; }

        public IOperationContext OperationContext { get; protected set; }

        public ILog Log { get; protected set; }

        public void Dispose()
        {
            this.myScope.Dispose();
        }
    }

    internal class OwnedLazyWithContext<TOwned> : OwnedLazyWithContextBase<TOwned>, IOwnedLazyWithContext<TOwned>
    {
        public OwnedLazyWithContext(ILifetimeScope scope) : base(scope)
        {
            this.Value = this.myScope.Resolve<Lazy<TOwned>>();
        }
    }

    internal class OwnedLazyWithContext<TArg1, TOwned> : OwnedLazyWithContextBase<TOwned>, IOwnedLazyWithContext<TArg1, TOwned>
    {
        public OwnedLazyWithContext(ILifetimeScope scope, TArg1 arg) : base(scope)
        {
            this.Value = this.myScope.Resolve<Lazy<TOwned>>(TypedParameter.From(arg));
        }
    }

    internal class OwnedLazyWithContext<TArg1, TArg2, TOwned> : OwnedLazyWithContextBase<TOwned>, IOwnedLazyWithContext<TArg1, TArg2, TOwned>
    {
        public OwnedLazyWithContext(ILifetimeScope scope, TArg1 arg1, TArg2 arg2) : base(scope)
        {
            this.Value = this.myScope.Resolve<Lazy<TOwned>>(TypedParameter.From(arg1), TypedParameter.From(arg2));
        }
    }
}