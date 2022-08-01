// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.EndpointManager;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.IP;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash;
using Microsoft.BridgeToKubernetes.Common.Socket;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.Extensions.Configuration;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;
using ClientConstants = Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.EndpointManager
{
    /// <summary>
    /// Dependency-Injection Service Configuration for the EndpointManager
    /// </summary>
    internal static class AppContainerConfig
    {
        /// <summary>
        /// Gets a container for running the EndpointManager
        /// </summary>
        /// <returns></returns>
        public static IContainer BuildContainer(string[] commandLineArgs)
        {
            var builder = new ContainerBuilder();

            // -------- Single Instance -------

            // The app
            builder.RegisterType<EndpointManager>()
                .AsSelf()
                .SingleInstance();

            // Configuration
            builder.Register(c =>
                {
                    var configBuilder = new ConfigurationBuilder()
                        .AddEnvironmentVariables();

                    return configBuilder.Build();
                })
                .As<IConfigurationRoot>()
                .SingleInstance();

            builder.RegisterType<CommandLineArgumentsManager>()
                .AsSelf()
                .OnActivating(e => e.Instance.ParseGlobalArgs(commandLineArgs))
                .SingleInstance();

            builder.RegisterType<ConsoleOutput>()
                .As<IConsoleOutput>()
                .SingleInstance();

            builder.RegisterType<Common.IO.Output.Console>()
                .As<IConsole>()
                .SingleInstance();

            // Settings
            builder.RegisterType<ApplicationInsightsLoggerConfig>() // By default, this type gets registered to send AI telemetry which can be disabled via an env variable
                .As<IApplicationInsightsLoggerConfig>()
                .SingleInstance();

            builder.RegisterType<WindowsSystemCheckService>()
                .As<IWindowsSystemCheckService>()
                .SingleInstance();

            // Versioning
            builder.RegisterType<AssemblyMetadataProvider>()
                .As<IAssemblyMetadataProvider>()
                .SingleInstance();

            builder.RegisterType<IPManager>()
                .As<IIPManager>()
                .SingleInstance();

            // -------- Instance Per Dependency -------
            builder.RegisterType<WebHostBuilder>()
                .As<IWebHostBuilder>()
                .InstancePerDependency();

            builder.RegisterType<HostsFileManager>()
                .As<IHostsFileManager>()
                .InstancePerDependency();

            builder.RegisterType<Socket>()
                .As<ISocket>()
                .InstancePerDependency();

            builder.RegisterType<ServiceController>()
                .As<IServiceController>()
                .InstancePerDependency();

            // -------- Modules -------

            // MacAddressHash
            builder.RegisterModule(new MacAddressHashModule());

            // Logging Module
            // Note: need to validate commandLineArgs before registering file logger
            if (commandLineArgs.Length != 4)
            {
                throw new ArgumentException($"Received {commandLineArgs.Length} args. Expected 4 args: username, socketFilePath, logFileDirectory and correlationId.");
            }
            builder.RegisterModule(new LoggingModule(ClientNames.EndpointManager)
            {
                EnableDefaultLogFile = true,
                LogFileDirectory = commandLineArgs[2], // We expect the user's logFileDirectory to get passed in on startup (admin temp path is resolved by default)
                ConsoleLoggingEnabled = true, // Users won't see the console for the EndpointManager during its execution
                ApplicationInsightsInstrumentationKey = (c) => ClientConstants.ApplicationInsights.InstrumentationKey,
                MacAddressHash = (c) => c.Resolve<MacInformationProvider>().MacAddressHash,
                OperationContextInitializer = (c, context) =>
                {
                    context.CorrelationId = commandLineArgs[3];
                }
            });

            // Common Module
            builder.RegisterModule<CommonModule>();

            return builder.Build();
        }
    }
}