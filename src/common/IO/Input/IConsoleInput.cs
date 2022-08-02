// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;

namespace Microsoft.BridgeToKubernetes.Common.IO.Input
{
    internal interface IConsoleInput
    {
        string ReadLine();

        TextReader InputReader { get; }
    }
}