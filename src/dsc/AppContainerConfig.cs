// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Net.Http;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Input;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash;
using Microsoft.BridgeToKubernetes.Exe.Commands;
using Microsoft.BridgeToKubernetes.Exe.Commands.Connect;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;
using ClientConstants = Microsoft.BridgeToKubernetes.Common.Constants;

namespace Microsoft.BridgeToKubernetes.Exe
{
    /// <summary>
    /// Dependency-Injection Service Configuration for the CLI
    /// </summary>
    internal static class AppContainerConfig
    {
        /// <summary>
        /// Gets a container for running the CLI
        /// </summary>
        /// <returns></returns>
        public static IContainer BuildContainer(string[] commandLineArgs)
        {
            var builder = new ContainerBuilder();

            // -------- Single Instance -------

            // The CLI app
            builder.RegisterType<CliApp>()
                   .AsSelf()
                   .SingleInstance();
            builder.Register<CommandLineApplication>(c =>
                    {
                        return new CommandLineApplication();
                    })
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

            builder.RegisterType<ConsoleOutput>()
                   .As<IConsoleOutput>()
                   .SingleInstance();

            builder.RegisterType<Common.IO.Output.Console>()
                   .As<IConsole>()
                   .SingleInstance();

            builder.RegisterType<ConsoleInput>()
                   .As<IConsoleInput>()
                   .SingleInstance();

            builder.RegisterType<ConsoleLauncher>()
                    .As<IConsoleLauncher>()
                    .SingleInstance();

            builder.RegisterType<CommandLineArgumentsManager>()
                   .AsSelf()
                   .OnActivating(e => e.Instance.ParseGlobalArgs(commandLineArgs))
                   .SingleInstance();

            builder.RegisterType<CommandsConfigurator>()
                   .AsSelf()
                   .SingleInstance();

            #region Commands

            builder.RegisterType<RootCommand>()
                   .AsSelf()
                   .SingleInstance();
            builder.RegisterType<ConnectCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<PrepConnectCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<CleanConnectCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<RoutingHeaderCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<RoutingSupportedCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<CheckCredentialsCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<ListIngressCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<ListNamespaceCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<ListServiceCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<ListContextCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();
            builder.RegisterType<RefreshCredentialsCommand>()
                   .As<ITopLevelCommand>()
                   .SingleInstance();

            #endregion Commands

            // Factories
            builder.Register(c =>
                    {
                        ManagementClientFactory.IsTelemetryEnabledCallback = c.Resolve<IApplicationInsightsLoggerConfig>().IsTelemetryEnabledCallback; // Set the IsTelemetryEnabled callback, in order to instantiate the SDK with the same telemetry collection settings as the CLI
                        ManagementClientFactory.IsLogFileEnabled = c.Resolve<IFileLoggerConfig>().LogFileEnabled;
                        return new ManagementClientFactory(c.Resolve<SourceUserAgentProvider>().UserAgent, c.Resolve<IOperationContext>().CorrelationId);
                    })
                   .As<IManagementClientFactory>()
                   .SingleInstance();
            builder.RegisterType<CliCommandOptionFactory>()
                   .As<ICliCommandOptionFactory>()
                   .SingleInstance();

            // Settings
            builder.RegisterType<KubectlImpl>()
                    .As<IKubectlImpl>()
                    .SingleInstance();

            builder.RegisterType<ApplicationInsightsLoggerConfig>() // This logger config will be used by the CLI code outside of the Client objects provided by the ManagementClientFactory
                   .As<IApplicationInsightsLoggerConfig>()          // By default, this type gets registered to send AI telemetry which can be disabled via an env variable or providing an expliciting callback function
                   .SingleInstance();

            // -------- Instance Per Dependency -------
            builder.RegisterType<HttpClient>()
                    .AsSelf()
                    .ExternallyOwned()
                    .InstancePerDependency();

            builder.RegisterType<WebHostBuilder>()
                    .As<IWebHostBuilder>()
                    .InstancePerDependency();

            builder.RegisterType<SdkErrorHandling>()
                   .As<ISdkErrorHandling>()
                   .InstancePerDependency();

            // -------- Modules -------

            // MacAddressHash
            builder.RegisterModule(new MacAddressHashModule());

            // Logging Module
            builder.RegisterModule(new LoggingModule(ClientNames.MindaroCli)
            {
                EnableDefaultLogFile = true,
                ApplicationInsightsInstrumentationKey = (c) => ClientConstants.ApplicationInsights.InstrumentationKey,
                MacAddressHash = (c) => c.Resolve<MacInformationProvider>().MacAddressHash,
                OperationContextInitializer = (c, context) =>
                {
                    context.LoggingProperties[Property.CommandId] = LoggingUtils.NewId();
                    context.CorrelationId = c.Resolve<IEnvironmentVariables>().CorrelationId + LoggingConstants.CorrelationIdSeparator + context.LoggingProperties[Property.CommandId];
                }
            });

            // Common Module
            builder.RegisterModule<CommonModule>();

            // Progress Module
            builder.RegisterModule<ProgressModule>();

            return builder.Build();
        }
    }
}