// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Utillities for logging
    /// </summary>
    public static class LoggingUtils
    {
        /// <summary>
        /// Generates a new unique identifier.
        /// This is usually used to extend the CorrelationId instead of using a full, longer Guid.
        /// </summary>
        /// <returns></returns>
        public static string NewId()
        {
            return Guid.NewGuid().ToString("D").Split("-").Last();
        }
    }
}