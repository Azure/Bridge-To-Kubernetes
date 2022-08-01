// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common
{
    internal enum ReleaseEnvironment
    {
        Production = 0,
        Staging,
        Development,
        Local,
        Test
    }
}