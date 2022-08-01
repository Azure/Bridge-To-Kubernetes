// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac.Extras.FakeItEasy;

namespace Microsoft.BridgeToKubernetes.TestHelpers
{
    public abstract class TestsBase : IDisposable
    {
        protected readonly AutoFake _autoFake = new AutoFake();

        public virtual void Dispose()
        {
            _autoFake.Dispose();
        }
    }
}