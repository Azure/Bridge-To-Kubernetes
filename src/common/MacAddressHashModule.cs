// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Autofac;
using Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash;
using Microsoft.BridgeToKubernetes.Common.PersistentProperyBag;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class MacAddressHashModule : Module
    {
        /// <summary>
        /// Registers a <see cref="MacInformationProvider"/> that then can be used to fetch the Machine MAcAddressHash
        /// Note:
        /// This is meant to be used on client only (CLI, SDK).
        /// </summary>
        /// <param name="builder"></param>
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ClientConfig>()
                   .As<IClientConfig>()
                   .SingleInstance()
                   .IfNotRegistered(typeof(ClientConfig));

            builder.RegisterType<VSCodeStorageReader>()
                   .AsSelf()
                   .SingleInstance()
                   .IfNotRegistered(typeof(VSCodeStorageReader));

            builder.RegisterType<MacInformationProvider>()
                   .AsSelf()
                   .SingleInstance()
                   .IfNotRegistered(typeof(MacInformationProvider));

            builder.RegisterType<VSRegistryPropertyReader>()
                   .As<IVSRegistryPropertyReader>()
                   .SingleInstance()
                   .IfNotRegistered(typeof(IVSRegistryPropertyReader));
        }
    }
}