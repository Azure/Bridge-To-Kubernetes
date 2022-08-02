// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models
{
    /// <summary>
    /// Status model for RoutingManager
    /// </summary>
    /// <remarks>!!!!! Any error message that needs to be returned to the user needs to be added to routingmanager\RoutingResource.resx !!!!!</remarks>
    public class RoutingStatus
    {
        /// <summary>
        /// public constructor
        /// </summary>
        /// <remarks>!!!!! Any error message that needs to be returned to the user needs to be added to routingmanager\RoutingResource.resx !!!!!</remarks>
        public RoutingStatus(bool? isConnected, string errorMessage = null)
        {
            IsConnected = isConnected;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Is the cluster setup for routing
        /// </summary>
        public bool? IsConnected { get; }

        /// <summary>
        /// Any error messages if cluster is not connected for routing
        /// </summary>
        public string ErrorMessage { get; }
    }
}