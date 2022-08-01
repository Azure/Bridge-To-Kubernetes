// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.EndpointManager
{
    /// <summary>
    /// General result of a request to the EndpointManager
    /// </summary>
    public class EndpointManagerResult
    {
        /// <summary>
        /// Indicates whether the request was successful
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// The error message if the request did not succeed
        /// </summary>
        public string ErrorMessage { get; set; }


        /// <summary>
        /// The string representation of a member of <see cref="Common.Constants.EndpointManager.Errors"/> if  the request did not succeed
        /// </summary>
        public string ErrorType { get; set; }
    }

    /// <summary>
    /// Result of a request to the EndpointManager, containing an object of type <typeparamref name="T"/>
    /// </summary>
    public class EndpointManagerResult<T> : EndpointManagerResult
    {
        /// <summary>
        /// The value returned by the EndpointManager
        /// </summary>
        public T Value { get; set; }
    }
}