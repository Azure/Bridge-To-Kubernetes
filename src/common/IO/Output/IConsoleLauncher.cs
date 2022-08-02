// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Diagnostics;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal interface IConsoleLauncher
    {
        Process LaunchTerminalWithEnv(IDictionary<string, string> envVars, string envScriptPath, bool performLaunch = true);
    }
}