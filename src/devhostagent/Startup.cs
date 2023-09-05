// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Text.Json.Serialization;
using Autofac;
using Microsoft.AspNetCore.Builder;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.DevHostAgent.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.BridgeToKubernetes.DevHostAgent
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
            services.AddResponseCompression();

            services.AddControllers()
                    .AllowNonPublicControllers()
                    .AddJsonOptions(o =>
                    {
                        o.JsonSerializerOptions.WriteIndented = true;
                        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                        o.JsonSerializerOptions.Converters.Add(new SystemTextJsonPatch.Converters.JsonPatchDocumentConverterFactory());
                    });

            services.AddSignalR(h =>
                {
                    h.EnableDetailedErrors = true;
                    h.KeepAliveInterval = TimeSpan.FromSeconds(5);
                })
                .AddMessagePackProtocol()
                .AddHubOptions<AgentExecutorHub>(options =>
                {
                    // To handle the below error in dotnet3
                    // InvalidDataException: The maximum message size of 32768B was exceeded. The message size can be configured in AddHubOptions.
                    options.MaximumReceiveMessageSize = null;

                    // We want to explicit set out arguments
                    options.DisableImplicitFromServicesParameters = true;
                });

            services.AddLogging(builder =>
            {
                builder.AddFilter("Microsoft", LogLevel.Warning)
                       .AddFilter("System", LogLevel.Warning)
                       .AddConsole();
            });
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            #region Single instance

            builder.RegisterType<Platform>()
                   .As<IPlatform>()
                   .IfNotRegistered(typeof(IPlatform))
                   .SingleInstance();

            #endregion Single instance

            #region Modules

            builder.RegisterModule(new LoggingModule(LoggingConstants.ClientNames.RemoteAgent)
            {
                EnableDefaultLogFile = true,
                ConsoleLoggingEnabled = true,
                ApplicationInsightsInstrumentationKey = (c) => Common.Constants.ApplicationInsights.InstrumentationKey,
                OperationContextInitializer = (c, context) =>
                {
                    context.CorrelationId = c.Resolve<IEnvironmentVariables>().CorrelationId;
                }
            });

            builder.RegisterModule<CommonModule>();

            #endregion Modules
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            var releaseEnvironment = app.ApplicationServices.GetService<IEnvironmentVariables>().ReleaseEnvironment;

            if (releaseEnvironment.IsDevelopmentEnvironment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseResponseCompression();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<AgentExecutorHub>("/api/signalr/agent", opt =>
                {
                    // increase buffer size to allow receiving bigger buffers for file sync
                    opt.ApplicationMaxBufferSize = 2000000;    // 2MB
                    opt.TransportMaxBufferSize = 2000000;      // 2MB
                });
            });
        }
    }
}