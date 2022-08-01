// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Exceptions
{
    internal class ConfigurationException : Exception
    {
        public ConfigurationException(string configParameterName, string additionalMessage = null)
            : base($"Configuration option '{configParameterName}' does not have a valid value!{(!string.IsNullOrEmpty(additionalMessage) ? " " + additionalMessage : string.Empty)}")
        { }
    }
}