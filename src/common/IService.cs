// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// A marker interface for injectable services in our codebase. Typically used with Reflection in some scenarios (<see cref="ServiceBase"/>),
    /// to determine whether objects are owned by the *container* vs a class itself.
    /// </summary>
    /// <remarks>It would be best to avoid adding any functionality here</remarks>
    internal interface IService
    {
    }
}