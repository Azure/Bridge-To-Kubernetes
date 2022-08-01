// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Autofac;
using Autofac.Core.Lifetime;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class LoggingModule : Module
    {
        protected bool ApplicationInsightsEnabled => ApplicationInsightsInstrumentationKey != null;
        public bool ConsoleLoggingEnabled { get; set; } = false;

        #region Log file settings

        /// <summary>
        /// The full path of the log file (overrides <see cref="LogFileDirectory"/> and <see cref="EnableDefaultLogFile"/>)
        /// </summary>
        public string FullLogFilePath { get; set; } = string.Empty;
        /// <summary>
        /// The directory to save log files, using the default filename format
        /// </summary>
        public string LogFileDirectory { get; set; } = string.Empty;
        /// <summary>
        /// Whether to save log files to the default directory with the default filename format
        /// </summary>
        public bool EnableDefaultLogFile { get; set; } = false;

        #endregion Log file settings

        public string ApplicationName { get; set; } = string.Empty;
        public Func<IComponentContext, string> ApplicationInsightsInstrumentationKey { get; set; }
        public Func<IComponentContext, string> MacAddressHash { get; set; }
        public Action<IComponentContext, IOperationContext> OperationContextInitializer { get; set; }

        internal LoggingModule(string applicationName)
        {
            ApplicationName = applicationName;
        }

        /// <summary>
        /// Registers <see cref="ILogger"/> based on this module's properties
        /// Notes:
        /// 1. A console logger will not be registered by default.
        /// 2. A <see cref="OperationContext"/> will be registered by default if no <see cref="IOperationContext"/> is registered.
        /// </summary>
        /// <param name="builder"></param>
        protected override void Load(ContainerBuilder builder)
        {
            // OperationContext
            builder.RegisterType<OperationContext>()
                   .OnActivating(c =>
                   {
                       c.Instance.Version = AssemblyVersionUtilities.GetCallingAssemblyInformationalVersion();
                       c.Instance.StartTime = DateTime.UtcNow;
                       c.Instance.LoggingProperties[Property.ApplicationName] = this.ApplicationName;
                       c.Instance.LoggingProperties[Property.DeviceOperatingSystem] = RuntimeInformation.OSDescription;
                       c.Instance.LoggingProperties[Property.Framework] = RuntimeInformation.FrameworkDescription;
                       if (MacAddressHash != null)
                       {
                           c.Instance.LoggingProperties[Property.MacAddressHash] = MacAddressHash(c.Context);
                       }
                   })
                   .As<IOperationContext>()
                   .IfNotRegistered(typeof(IOperationContext))
                   .InstancePerLifetimeScope();

            builder.RegisterType<AssemblyMetadataProvider>()
                   .As<IAssemblyMetadataProvider>()
                   .IfNotRegistered(typeof(IAssemblyMetadataProvider))
                   .SingleInstance();

            builder.RegisterType<SourceUserAgentProvider>()
                   .WithParameter(TypedParameter.From(this.ApplicationName))
                   .AsSelf()
                   .IfNotRegistered(typeof(SourceUserAgentProvider))
                   .SingleInstance();

            // This allows to automatically carry the context down Autofac LifetimeScopes
            builder.RegisterBuildCallback(c =>
            {
                void inheritContext(object x, LifetimeScopeBeginningEventArgs y)
                {
                    var oldContext = ((ILifetimeScope)x).Resolve<IOperationContext>();
                    var newContext = y.LifetimeScope.Resolve<IOperationContext>();
                    newContext.Inherit(oldContext);
                    y.LifetimeScope.ChildLifetimeScopeBeginning += inheritContext;
                };
                c.ChildLifetimeScopeBeginning += inheritContext;
            });

            // Add custom properties to the Operation Context
            builder.RegisterBuildCallback(c =>
            {
                var operationContext = c.Resolve<IOperationContext>();
                var environmentVariables = c.Resolve<IEnvironmentVariables>();

                operationContext.Version = c.Resolve<IAssemblyMetadataProvider>().AssemblyVersion;
                operationContext.LoggingProperties[Property.ProcessId] = Process.GetCurrentProcess().Id;
                operationContext.LoggingProperties[Property.TargetEnvironment] = environmentVariables.ReleaseEnvironment.ToString();
                operationContext.UserAgent = c.Resolve<SourceUserAgentProvider>().UserAgent;

                // Add any application-specific Operation Context properties.
                this.OperationContextInitializer?.Invoke(c, operationContext);
            });

            // Note:
            // Logger Writers are single instance since they don't reference context and are only outputting to a single TelemetryClient or File.
            // Example of writers are ApplicationInsights TelemetryClient and ThreadSafeFileWriter since they are the last class of the pipeline that finally send or persist the data.
            //
            // Logger are InstancePerLifetimeScope since they point to an InstancePerLifeTimeScope ContextResolver.
            // Loggers are middleware classes between the standard ILog API and the specific implementation of the logger writer they rely on. E.g. ApplicationInsightsLogger wraps TelemetryClient

            if (this.EnableDefaultLogFile ||
                !string.IsNullOrWhiteSpace(this.LogFileDirectory) ||
                !string.IsNullOrWhiteSpace(this.FullLogFilePath))
            {
                string defaultFileName = $"{Constants.Product.NameAbbreviation.ToLowerInvariant()}-{this.ApplicationName.ToLowerInvariant()}-{DateTime.UtcNow.ToString("yyyy-MM-dd-HH-mm-ss")}-{Process.GetCurrentProcess().Id}.txt";
                string logFilePath = Path.Combine(Path.GetTempPath(), Constants.DirectoryName.Logs, defaultFileName);
                if (!string.IsNullOrWhiteSpace(this.LogFileDirectory))
                {
                    logFilePath = Path.Combine(this.LogFileDirectory, defaultFileName);
                }
                if (!string.IsNullOrWhiteSpace(this.FullLogFilePath))
                {
                    logFilePath = this.FullLogFilePath;
                }

                builder.RegisterType<ThreadSafeFileWriter>()
                       .WithParameter(TypedParameter.From(logFilePath))
                       .Named<IThreadSafeFileWriter>(logFilePath)
                       .SingleInstance();

                builder.RegisterType<FileLoggerConfig>()
                       .WithParameter(TypedParameter.From(this.ApplicationName))
                       .As<IFileLoggerConfig>()
                       .IfNotRegistered(typeof(IFileLoggerConfig))
                       .SingleInstance();

                builder.Register(c => new FileLogger(c.Resolve<IOperationContext>(), c.ResolveNamed<IThreadSafeFileWriter>(logFilePath), c.Resolve<IFileLoggerConfig>()))
                       .As<IFileLogger>()
                       .As<ILogger>()
                       .InstancePerLifetimeScope();
            }

            if (this.ConsoleLoggingEnabled)
            {
                builder.RegisterType<EnvironmentVariables>()
                       .As<IEnvironmentVariables>()
                       .IfNotRegistered(typeof(IEnvironmentVariables))
                       .SingleInstance();

                builder.RegisterType<ConsoleLoggerConfig>()
                       .WithParameter(TypedParameter.From(this.ApplicationName))
                       .As<IConsoleLoggerConfig>()
                       .IfNotRegistered(typeof(IConsoleLoggerConfig))
                       .SingleInstance();

                builder.RegisterType<ConsoleLogger>()
                       .As<ILogger>()
                       .PreserveExistingDefaults()
                       .InstancePerLifetimeScope();
            }

            if (this.ApplicationInsightsEnabled)
            {
                builder.RegisterType<EnvironmentVariables>()
                       .As<IEnvironmentVariables>()
                       .IfNotRegistered(typeof(IEnvironmentVariables))
                       .SingleInstance();

                builder.Register<TelemetryConfiguration>(c =>
                           new TelemetryConfiguration
                           {
                               InstrumentationKey = this.ApplicationInsightsInstrumentationKey(c),
                               TelemetryChannel = new ApplicationInsights.Channel.InMemoryChannel()
                           })
                       .AsSelf() // May override a default registration
                       .SingleInstance();

                // Telemetry Initializer
                builder.RegisterType<OperationContextTelemetryInitializer>()
                       .As<IOperationContextTelemetryInitializer>()
                       .InstancePerLifetimeScope();

                builder.RegisterType<ApplicationInsightsLoggerConfig>()     // By default, this type gets registered to send AI telemetry which can be disabled via an env variable or providing an expliciting callback function
                       .As<IApplicationInsightsLoggerConfig>()
                       .IfNotRegistered(typeof(IApplicationInsightsLoggerConfig))
                       .SingleInstance();

                builder.Register<TelemetryClient>(c =>
                       {
                           var telClient = new TelemetryClient(c.Resolve<TelemetryConfiguration>());
                           var operationContext = c.Resolve<IOperationContext>();

                           telClient.InstrumentationKey = this.ApplicationInsightsInstrumentationKey(c);

                           try
                           {
                               telClient.Context.Device.OperatingSystem = (string)operationContext.LoggingProperties[Property.DeviceOperatingSystem];
                           }
                           catch { }
                           return telClient;
                       })
                       .AsSelf() // May override a default registration
                       .SingleInstance();

                builder.RegisterType<ApplicationInsightsLogger>()
                       .WithParameter(TypedParameter.From(this))
                       .OnActivating(e => e.Instance.ShouldScramblePii = e.Context.Resolve<IEnvironmentVariables>().ReleaseEnvironment.IsProduction())
                       .As<ILogger>()
                       .AsSelf()
                       .InstancePerLifetimeScope();
            }

            builder.RegisterType<Log>()
                   .As<ILog>()
                   .InstancePerLifetimeScope()
                   .IfNotRegistered(typeof(ILog));
        }
    }
}