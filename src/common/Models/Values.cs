// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models
{
    /// <summary>
    /// Represents a Helm values file
    /// </summary>
    public class Values
    {
        /// <summary>
        /// Relative path to the values file
        /// </summary>
        public string File { get; set; }

        /// <summary>
        /// False if the values file is required, otherwise true
        /// </summary>
        public bool Optional { get; set; }
    }
}