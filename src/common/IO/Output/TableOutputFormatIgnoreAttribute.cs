// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    /// <summary>
    /// Marks the property so that it is ignored by the ConsoleOutput when using the Table output format.
    /// </summary>
    internal class TableOutputFormatIgnore : Attribute
    {
    }
}