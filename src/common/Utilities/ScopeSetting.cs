// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    internal enum ScopeSetting
    {
        InstancePerDependency = 0,
        InstancePerLifetimeScope,
        SingleInstance
    }
}