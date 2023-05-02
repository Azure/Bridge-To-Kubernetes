// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.IP;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.LocalAgent
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
            services.AddSingleton<LocalAgentApp>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterType<LocalAgentApp>()
                   .AsSelf()
                   .As<IHostedService>()
                   .SingleInstance();

            builder.RegisterType<IPManager>()
                .As<IIPManager>()
                .SingleInstance();

            builder.RegisterType<PortMappingManager>()
                .As<IPortMappingManager>()
                .SingleInstance();

            builder.RegisterType<ConsoleOutput>()
                   .As<IConsoleOutput>()
                   .SingleInstance();

            builder.RegisterType<Common.IO.Output.Console>()
                .As<IConsole>()
                .SingleInstance();

            builder.RegisterType<CommandLineArgumentsManager>()
                .AsSelf()
                .OnActivating(e => e.Instance.ParseGlobalArgs(new string[] { }))
                .SingleInstance();

            // Factories
            builder.Register(c =>
            {
                ManagementClientFactory.IsTelemetryEnabledCallback = c.Resolve<IApplicationInsightsLoggerConfig>().IsTelemetryEnabledCallback; // Set the IsTelemetryEnabled callback, in order to instantiate the SDK with the same telemetry collection settings as the CLI
                ManagementClientFactory.IsLogFileEnabled = c.Resolve<IFileLoggerConfig>().LogFileEnabled;
                return new ManagementClientFactory(c.Resolve<SourceUserAgentProvider>().UserAgent, c.Resolve<IOperationContext>().CorrelationId);
            })
                   .As<IManagementClientFactory>()
                   .SingleInstance();

            builder.RegisterModule(new LoggingModule(ClientNames.LocalAgent)
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