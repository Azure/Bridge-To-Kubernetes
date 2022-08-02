// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.BridgeToKubernetes.TestHelpers
{
    public static class InterfaceImplementationTestHelpers
    {
        public static void EnsureInterfaceAndImplementationInSync(Type interfaceType, Type implementationType)
        {
            Assert.True(interfaceType.IsInterface);
            Assert.True(!implementationType.IsInterface && implementationType.IsClass);
            Assert.True(interfaceType.IsAssignableFrom(implementationType));

            var interfaceMethods = interfaceType.GetMethods();
            var implementationMethods = implementationType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            var joinedMethods = from inter in interfaceMethods
                                join impl in implementationMethods on inter.Name equals impl.Name
                                select new { Interface = inter, Implementation = impl };
            Assert.Equal(interfaceMethods.Count(), joinedMethods.Count());
            Assert.True(joinedMethods.All(x => x.Interface != null && x.Implementation != null));

            foreach (var join in joinedMethods)
            {
                Assert.Equal(join.Interface.ReturnType, join.Implementation.ReturnType);

                var interfaceParams = join.Interface.GetParameters();
                var implementationParams = join.Implementation.GetParameters();
                Assert.Equal(interfaceParams.Length, implementationParams.Length);
                Assert.True(interfaceParams.SequenceEqual(implementationParams, new ParameterInfoComparer()), $"{join.Interface.Name} has non-matching parameters!");
            }
        }
    }
}