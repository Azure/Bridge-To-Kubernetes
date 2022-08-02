// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common
{
    internal static class ReleaseEnvironmentExtensions
    {
        /// <summary>
        /// Determines whether we're currently running in Production
        /// </summary>
        /// <returns>True if <paramref name="env"/> is <see cref="ReleaseEnvironment.Production"/>, otherwise false</returns>
        public static bool IsProduction(this ReleaseEnvironment env)
            => env == ReleaseEnvironment.Production;

        /// <summary>
        /// Determines whether we're currently running in a development (not Production or Staging) environment
        /// </summary>
        /// <returns>True if <paramref name="env"/> is a development environment, otherwise false</returns>
        public static bool IsDevelopmentEnvironment(this ReleaseEnvironment env)
            => env.IsIn(ReleaseEnvironment.Development, ReleaseEnvironment.Test, ReleaseEnvironment.Local);
        }
}