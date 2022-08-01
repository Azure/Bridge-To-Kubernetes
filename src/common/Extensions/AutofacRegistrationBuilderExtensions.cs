// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Autofac.Builder;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Autofac
{
    /// <summary>
    /// Provides extension methods for the Autofac builder
    /// </summary>
    internal static class AutofacRegistrationBuilderExtensions
    {
        /// <summary>
        /// Shortcut for IInitialize classes, to ensure they are fully initialized before they are consumed
        /// </summary>
        /// <typeparam name="X"></typeparam>
        /// <typeparam name="Y"></typeparam>
        /// <typeparam name="Z"></typeparam>
        /// <param name="builder"></param>
        /// <param name="useIsolatedSynchronizationContext"></param>
        /// <returns></returns>
        public static IRegistrationBuilder<X, Y, Z> InitializeOnActivation<X, Y, Z>(this IRegistrationBuilder<X, Y, Z> builder, bool useIsolatedSynchronizationContext = true) where X : ServiceBase
        {
            return builder.OnActivating(e =>
            {
                if (useIsolatedSynchronizationContext)
                {
                    AsyncHelpers.RunSync(() => e.Instance.InitializeAsync());
                }
                else
                {
                    e.Instance.InitializeAsync().Wait();
                }
            });
        }

        /// <summary>
        /// Begins a new lifetime scope with the provided logger/context
        /// </summary>
        public static ILifetimeScope BeginLifetimeScopeWithContext(this ILifetimeScope scope, ILog log)
        {
            return scope.BeginLifetimeScope(b =>
            {
                b.RegisterInstance(log)
                    .As<ILog>()
                    .SingleInstance();
                b.RegisterInstance(log.OperationContext)
                    .As<IOperationContext>()
                    .SingleInstance();
            });
        }
    }
}