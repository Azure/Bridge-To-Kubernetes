// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Net.Http;
using Autofac;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash;
using Microsoft.BridgeToKubernetes.Common.PortForward;
using Microsoft.BridgeToKubernetes.Common.Restore;
using Microsoft.BridgeToKubernetes.Common.Socket;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Client.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.Connect;
using Microsoft.BridgeToKubernetes.Library.Connect.Environment;
using Microsoft.BridgeToKubernetes.Library.EndpointManagement;
using Microsoft.BridgeToKubernetes.Library.Extensions;
using Microsoft.BridgeToKubernetes.Library.LocalAgentManagement;
using Microsoft.BridgeToKubernetes.Library.ManagementClients;
using Microsoft.BridgeToKubernetes.Library.ServiceClients;
using Microsoft.BridgeToKubernetes.Library.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.Library
{
    internal static class AppContainerConfig
    {
        /// <summary>
        /// Static constructor
        /// </summary>
        static AppContainerConfig()
        {
            var builder = new ContainerBuilder();

            #region SingleInstance

            builder.RegisterType<KubeConfigManagementClient>()
                   .AsSelf()
                   .As<IKubeConfigManagementClient>()
                   .SingleInstance();

            builder.RegisterType<KubectlImpl>()
                   .As<IKubectlImpl>()
                   .SingleInstance();

            builder.RegisterType<K8sClientFactory>()
                   .As<IK8sClientFactory>()
                   .SingleInstance();

            // Port forwarding
            builder.RegisterType<KubernetesPortForwardManager>()
                   .As<IKubernetesPortForwardManager>()
                   .SingleInstance();

            builder.RegisterType<StreamManager>()
                   .As<IStreamManager>()
                   .SingleInstance();

            builder.RegisterType<EnvironmentVariables>()
                   .As<IEnvironmentVariables>()
                   .SingleInstance();

            builder.RegisterType<EndpointManagementClient>()
                   .AsSelf()
                   .As<IEndpointManagementClient>()
                   .SingleInstance();

            builder.RegisterType<ImageProvider>()
                   .As<IImageProvider>()
                   .SingleInstance();

            builder.RegisterType<LinuxEndpointManagerLauncher>().AsSelf();
            builder.RegisterType<WindowsEndpointManagerLauncher>().AsSelf();
            builder.RegisterType<OsxEndpointManagerLauncher>().AsSelf();

            builder.Register<IEndpointManagerLauncher>(c =>
            {
                return c.Resolve<IPlatform>() switch
                {
                     var v when v.IsWindows => c.Resolve<WindowsEndpointManagerLauncher>(),
                     var v when v.IsLinux => c.Resolve<LinuxEndpointManagerLauncher>(),
                     var v when v.IsOSX => c.Resolve<OsxEndpointManagerLauncher>(),
                     _ => throw new InvalidOperationException($"Unsupported operating system: {c.Resolve<IPlatform>()}"),
                 };
             }).As<IEndpointManagerLauncher>().SingleInstance();

            // Versioning
            builder.RegisterType<AssemblyMetadataProvider>()
                .As<IAssemblyMetadataProvider>()
                .SingleInstance();

            #endregion SingleInstance

            #region InstancePerLifetimeScope

            builder.RegisterType<WorkloadInformationProvider>()
                   .As<IWorkloadInformationProvider>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<KubernetesRemoteEnvironmentManager>()
                   .As<IRemoteEnvironmentManager>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<LocalEnvironmentManager>()
                   .As<ILocalEnvironmentManager>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<LocalAgentManager>()
                   .As<ILocalAgentManager>()
                   .InstancePerLifetimeScope();
            builder.RegisterType<PortMappingManager>()
                    .As<IPortMappingManager>()
                    .InstancePerLifetimeScope();

            #endregion InstancePerLifetimeScope

            #region InstancePerDependency

            // Service clients

            // Service client exception strategies
            builder.RegisterType<KubernetesRestClientExceptionStrategy>()
                   .AsSelf()
                   .InstancePerDependency();

            // Management Clients
            builder.RegisterType<ConnectManagementClient>()
                   .AsSelf()
                   .As<IConnectManagementClient>()
                   .InstancePerDependency();

            builder.RegisterType<KubernetesManagementClient>()
                   .AsSelf()
                   .As<IKubernetesManagementClient>()
                   .InstancePerDependency();

            builder.RegisterType<RoutingManagementClient>()
                   .AsSelf()
                   .As<IRoutingManagementClient>()
                   .InstancePerDependency();

            // Management client exception strategies
            builder.RegisterType<ManagementClientExceptionStrategy>()
                   .AsSelf()
                   .InstancePerDependency();

            builder.RegisterType<DevHostAgentExecutorClient>()
                   .As<IDevHostAgentExecutorClient>()
                   .InstancePerDependency();

            builder.RegisterType<KubernetesClient>()
                   .As<IKubernetesClient>()
                   .InstancePerDependency();

            builder.RegisterType<ReversePortForwardManager>()
                   .As<IReversePortForwardManager>()
                   .InstancePerDependency();
            builder.RegisterType<ServicePortForwardManager>()
                   .As<IServicePortForwardManager>()
                   .InstancePerDependency();

            builder.RegisterType<Socket>()
                   .As<ISocket>()
                   .InstancePerDependency();

            builder.RegisterType<PortListener>()
                   .As<IPortListener>()
                   .InstancePerDependency();

            #endregion InstancePerDependency

            #region Connect

            builder.RegisterType<ResourceNamingService>()
                   .As<IResourceNamingService>()
                   .InstancePerDependency();

            builder.RegisterType<HttpClient>()
                   .AsSelf()
                   .ExternallyOwned()
                   .InstancePerDependency();

            builder.RegisterType<WorkloadRestorationService>()
                   .As<IWorkloadRestorationService>()
                   .InstancePerDependency();

            builder.RegisterType<RemoteRestoreJobDeployer>()
                   .As<IRemoteRestoreJobDeployer>()
                   .InstancePerDependency();

            builder.RegisterType<LocalProcessConfig>()
                   .As<ILocalProcessConfig>()
                   .InstancePerDependency();

            builder.RegisterType<RemoteContainerConnectionDetailsResolver>()
                   .AsSelf()
                   .InstancePerDependency();

            builder.RegisterGeneric(typeof(OwnedLazyWithContext<>))
                   .As(typeof(IOwnedLazyWithContext<>))
                   .InstancePerDependency();
            builder.RegisterGeneric(typeof(OwnedLazyWithContext<,>))
                   .As(typeof(IOwnedLazyWithContext<,>))
                   .InstancePerDependency();
            builder.RegisterGeneric(typeof(OwnedLazyWithContext<,,>))
                   .As(typeof(IOwnedLazyWithContext<,,>))
                   .InstancePerDependency();

            #endregion Connect

            #region Modules/build callbacks

            // MacAddressHash
            builder.RegisterModule(new MacAddressHashModule());

            // Logging Module
            builder.RegisterModule(new LoggingModule(ClientNames.Library)
            {
                EnableDefaultLogFile = true,
                ApplicationInsightsInstrumentationKey = (c) => Common.Constants.ApplicationInsights.InstrumentationKey,
                MacAddressHash = (c) => c.Resolve<MacInformationProvider>().MacAddressHash
            });

            // Common Module
            builder.RegisterModule<CommonModule>();

            // Progress Module
            builder.RegisterModule<ProgressModule>();

            #endregion Modules/build callbacks

            RootScope = builder.Build();
        }

        public static ILifetimeScope RootScope { get; }

        /// <summary>
        /// Creates an object inside its own Lifetime scope, that automatically disposes the scope on Dispose()
        /// </summary>
        /// <typeparam name="T">The type to create</typeparam>
        /// <param name="factory">The factory of the type to create, this is helpful to pass constructor parameters</param>
        /// <param name="editScopeFunc">Used to alter the normal scope of autofac. E.g. to pass a specific KubernetesClient</param>
        /// <returns></returns>
        public static T CreateOwnedObjectFromScope<T>(Func<ILifetimeScope, T> factory, Action<ContainerBuilder> editScopeFunc = null) where T : ServiceBase
        {
            if (editScopeFunc == null)
            {
                editScopeFunc = b => { };
            }
            var scope = RootScope.BeginLifetimeScope(editScopeFunc);
            var ownedObject = factory(scope);
            ownedObject.Disposing += (x, y) =>
            {
                scope.Dispose(); // Dispose the ownedObject's entire lifetime scope
            };
            return ownedObject;
        }
    }
}