// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Autofac;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Serialization;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Registers commonly used types from common library
    /// </summary>
    internal class CommonModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<EnvironmentVariables>()
                   .As<IEnvironmentVariables>()
                   .IfNotRegistered(typeof(IEnvironmentVariables))
                   .SingleInstance();

            builder.RegisterAdapter<IEnvironmentVariables, ReleaseEnvironment>(env => env.ReleaseEnvironment)
                   .As<ReleaseEnvironment>()
                   .IfNotRegistered(typeof(ReleaseEnvironment))
                   .InstancePerDependency();

            builder.RegisterType<Platform>()
                   .As<IPlatform>()
                   .IfNotRegistered(typeof(IPlatform))
                   .SingleInstance();

            builder.RegisterType<PathUtilities>()
                   .As<IPathUtilities>()
                   .IfNotRegistered(typeof(IPathUtilities))
                   .SingleInstance();

            builder.RegisterType<FileSystem>()
                   .As<IFileSystem>()
                   .IfNotRegistered(typeof(IFileSystem))
                   .SingleInstance();

            builder.RegisterType<JsonSerializer>()
                   .As<IJsonSerializer>()
                   .IfNotRegistered(typeof(IJsonSerializer))
                   .SingleInstance();
        }
    }
}