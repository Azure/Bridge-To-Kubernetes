// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// ContainerVolumeMappingInfo describes a volume mapped in the container.
    /// </summary>
    public class ContainerVolumeMountInfo
    {
        /// <summary>
        /// Container name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Local volume name
        /// </summary>
        public string LocalVolumeName { get; set; }

        /// <summary>
        /// Path within the volume
        /// </summary>
        public string SubPath { get; set; }

        /// <summary>
        /// Location to mount the volume in the container
        /// </summary>
        public string ContainerPath { get; set; }
    }
}