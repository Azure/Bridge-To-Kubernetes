// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Exceptions
{
    /// <summary>
    /// This interface identifies exceptions that are raised based on an error in our product that needs to be communicated to the user.
    /// This includes cases where our product was used incorrectly or the underlying environment is in a bad or unexpected state
    /// It is not intended for technical errors in the product.
    /// Examples include incorrect configuration, name validation errors, build errors, lack of network, cluster in bad state, etc.
    /// </summary>
    public interface IUserVisibleExceptionReporter
    {
        /// <summary>
        /// Message describing the error
        /// </summary>
        string Message { get; }
    }
}