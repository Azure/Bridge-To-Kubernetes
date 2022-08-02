// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Commands;

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    /// <summary>
    /// A marker interface for top-level commands
    /// </summary>
    internal interface ITopLevelCommand : ICommand
    {
    }
}