// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Reflection;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.Extensions.Hosting;
using static Microsoft.BridgeToKubernetes.Common.Logging.LoggingConstants;

namespace Microsoft.BridgeToKubernetes.RoutingManager
{
    public class Program
    {
        public static void Main(string[] args)
        {
            AspNetCoreRunner.RunHost(
                webhostAssembly: Assembly.GetExecutingAssembly(),
                hostBuilder: CreateHostBuilder,
                args: args,
                userAgent: $"{ClientNames.RoutingManager}/{AssemblyVersionUtilities.GetCallingAssemblyInformationalVersion()}");
        }

        public static IHost CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseUrls($"http://localhost:{Common.Constants.Routing.RoutingManagerPort}");
                    webBuilder.UseStartup<Startup>();
                })
                .Build();
    }
}