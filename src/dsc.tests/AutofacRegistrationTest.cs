// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Autofac;
using Autofac.Core;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Exe.Tests
{
    public class AutofacRegistrationTest
    {
        private readonly IContainer _container = AppContainerConfig.BuildContainer(new string[0]);

        [Fact]
        public void ResolveAllServiceTypes()
        {
            var serviceTypes = _container.ComponentRegistry.Registrations.SelectMany(x => x.Services).OfType<IServiceWithType>().Select(x => x.ServiceType);

            foreach (var serviceType in serviceTypes)
            {
                if (serviceType == typeof(IThreadSafeFileWriter)) // IThreadSafeFileWriter is registered by name, so it cannot be resolved by type
                {
                    continue;
                }

                Assert.NotNull(_container.Resolve(serviceType));
                Assert.NotNull(_container.Resolve(typeof(Lazy<>).MakeGenericType(serviceType)));
                Assert.NotNull(_container.Resolve(typeof(IEnumerable<>).MakeGenericType(serviceType)));
                Assert.NotNull(_container.Resolve(typeof(Func<,>).MakeGenericType(typeof(object), serviceType)));
            }
        }
    }
}