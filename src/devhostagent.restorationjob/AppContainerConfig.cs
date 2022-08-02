// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using System.Net.Http;
using Autofac;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Restore;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.RestorationJob
{
    internal static class AppContainerConfig
    {
        public static IContainer BuildContainer()
        {
            var builder = new ContainerBuilder();

            #region SingleInstance

            builder.RegisterType<RestorationJobApp>()
                   .AsSelf()
                   .As<AppBase>()
                   .SingleInstance();

            builder.RegisterType<HttpClient>()
                   .AsSelf()
                   .SingleInstance();

            builder.RegisterType<RestorationJobEnvironmentVariables>()
                   .As<IRestorationJobEnvironmentVariables>()
                   .As<IEnvironmentVariables>()
                   .SingleInstance();

            #endregion SingleInstance

            #region InstancePerDependency

            builder.RegisterType<WorkloadRestorationService>()
                   .As<IWorkloadRestorationService>()
                   .InstancePerDependency();

            builder.RegisterType<RemoteRestoreJobCleaner>()
                   .As<IRemoteRestoreJobCleaner>()
                   .InstancePerDependency();

            const string KubernetesClientBase = "base";
            builder.RegisterType<KubernetesClient>()
                   .Named<IKubernetesClient>(KubernetesClientBase)
                   .InstancePerDependency();
            builder.Register(c => c.ResolveNamed<KubernetesClient.InClusterFactory>(KubernetesClientBase).Invoke(useInClusterConfig: true))
                   .As<IKubernetesClient>()
                   .InstancePerDependency();
            builder.RegisterType<K8sClientFactory>()
                   .As<IK8sClientFactory>()
                   .InstancePerDependency();

            #endregion InstancePerDependency

            #region Modules

            builder.RegisterModule(new LoggingModule(LoggingConstants.ClientNames.RestorationJob)
            {
                ConsoleLoggingEnabled = true,
                LogFileDirectory = Path.Combine(new PathUtilities(new Platform()).GetEntryAssemblyDirectoryPath(), "logs"),
                ApplicationInsightsInstrumentationKey = (c) => Constants.ApplicationInsights.InstrumentationKey,
                OperationContextInitializer = (c, context) =>
                {
                    context.CorrelationId = c.Resolve<IEnvironmentVariables>().CorrelationId;
                }
            });

            builder.RegisterModule<CommonModule>();

            #endregion Modules

            return builder.Build();
        }
    }
}