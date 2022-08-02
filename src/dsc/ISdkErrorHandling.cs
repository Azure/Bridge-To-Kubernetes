// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Exe
{
    /// <summary>
    /// Class for handling common SDK errors
    /// </summary>
    internal interface ISdkErrorHandling
    {
        /// <summary>
        /// Attempts to handle common/known SDK exceptions. If the return value is true, assume the proper message has already been printed to the user.
        /// </summary>
        bool TryHandleKnownException(Exception e, string failedDependencyName, out string failureReason, bool displayUnkownErrors = false);

        /// <summary>
        /// Attempts to handle common/known SDK exceptions. If the return value is not null or empty, assume the proper message has already been printed to the user.
        /// </summary>
        /// <returns>The string message that was printed to console out, or null if not identified</returns>
        string TryHandleKnownExceptionAndGetMessage(Exception e, string failedDependencyName, bool displayUnkownErrors = false);
    }
}