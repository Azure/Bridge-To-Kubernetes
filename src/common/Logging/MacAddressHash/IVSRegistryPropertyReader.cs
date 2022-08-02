// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash
{
    internal interface IVSRegistryPropertyReader
    {
        object GetProperty(string propertyName);
    }
}