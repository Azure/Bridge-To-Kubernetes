// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Reflection;
using Autofac.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.DevHostAgent;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.Extensions.Hosting;

namespace Microsoft.BridgeToKubernetes.DevHostAgent
{
    internal class Program
    {
        public static void Main(string[] args)
            => AspNetCoreRunner.RunHost(
                webhostAssembly: Assembly.GetExecutingAssembly(),
                hostBuilder: CreateHost,
                args: args,
                userAgent: $"{LoggingConstants.ClientNames.RemoteAgent}/{AssemblyVersionUtilities.GetCallingAssemblyInformationalVersion()}");

        /// <summary>
        /// Creates devhostagent Host
        /// </summary>
        public static IHost CreateHost(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseUrls($"http://*:{DevHostConstants.DevHostAgent.Port}/");
                })
                .Build();
    }
}