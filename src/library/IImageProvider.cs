// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library
{
    internal interface IImageProvider
    {
        string DevHostImage { get; }
        string DevHostRestorationJobImage { get; }
        string RoutingManagerImage { get; }
    }
}