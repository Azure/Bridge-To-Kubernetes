// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Services.Kubernetes;
using Microsoft.BridgeToKubernetes.RoutingManager.Configuration;
using Microsoft.BridgeToKubernetes.RoutingManager.Envoy;
using Microsoft.BridgeToKubernetes.RoutingManager.TriggerConfig;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddSingleton<RoutingManagerApp>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterType<RoutingManagerApp>()
                   .AsSelf()
                   .As<IHostedService>()
                   .SingleInstance();

            builder.RegisterType<RoutingManagerConfig>()
                   .As<IRoutingManagerConfig>()
                   .SingleInstance();

            const string KubernetesWatcherBase = "watcherbase";
            builder.RegisterType<KubernetesWatcher>()
                    .Named<IKubernetesWatcher>(KubernetesWatcherBase)
                    .SingleInstance();
            builder.Register(c => c.ResolveNamed<KubernetesWatcher.InClusterFactory>(KubernetesWatcherBase).Invoke(useInClusterConfig: true))
                   .As<IKubernetesWatcher>()
                   .SingleInstance();

            const string KubernetesClientBase = "base";
            builder.RegisterType<KubernetesClient>()
                   .Named<IKubernetesClient>(KubernetesClientBase)
                   .InstancePerDependency();
            builder.Register(c => c.ResolveNamed<KubernetesClient.InClusterFactory>(KubernetesClientBase).Invoke(useInClusterConfig: true, kubectlFilePath: "/app/kubectl/linux/kubectl"))
                   .As<IKubernetesClient>()
                   .InstancePerDependency();
            builder.RegisterType<K8sClientFactory>()
                   .As<IK8sClientFactory>()
                   .InstancePerDependency();

            builder.RegisterType<RoutingStateEstablisher>()
                   .AsSelf()
                   .InstancePerDependency();

            builder.RegisterType<IngressTriggerConfig>()
                   .As<ITriggerConfig>()
                   .InstancePerDependency();

            builder.RegisterType<PodTriggerConfig>()
                   .As<ITriggerConfig>()
                   .InstancePerDependency();

            builder.RegisterType<EnvoyConfigBuilder>()
                   .AsSelf()
                   .InstancePerDependency();

            builder.RegisterModule(new LoggingModule(ClientNames.RoutingManager)
            {
                ConsoleLoggingEnabled = true,
                LogFileDirectory = "/var/log",
                ApplicationInsightsInstrumentationKey = (c) => Common.Constants.ApplicationInsights.InstrumentationKey,
                OperationContextInitializer = (c, context) =>
                {
                    context.CorrelationId = c.Resolve<IEnvironmentVariables>().CorrelationId;
                }
            });

            builder.RegisterModule<CommonModule>();
        }
    }
}