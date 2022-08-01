// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    public interface IEnvironmentToken
    {
        /// <summary>
        /// Referenced entity name e.g. FileName, ServiceName, VolumeName
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Evaluate the environment token and return the string value
        /// </summary>
        /// <returns></returns>
        string Evaluate();
    }
}