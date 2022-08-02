// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Autofac;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common
{
    /// <summary>
    /// Registers types needed to resolve a progress reporter
    /// </summary>
    internal class ProgressModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CommandLineArgumentsManager>()
                   .AsSelf()
                   .IfNotRegistered(typeof(CommandLineArgumentsManager))
                   .SingleInstance();

            builder.RegisterType<IO.Output.Console>()
                   .As<IConsole>()
                   .IfNotRegistered(typeof(IConsole))
                   .SingleInstance();

            builder.RegisterType<ConsoleOutput>()
                   .As<IConsoleOutput>()
                   .IfNotRegistered(typeof(IConsoleOutput))
                   .SingleInstance();

            builder.Register(c =>
                    {
                        var consoleOutput = c.Resolve<IConsoleOutput>();
                        return new SerializedProgress<ProgressUpdate>((progressUpdate) =>
                        {
                            // Clients can choose to handle the progress update and display the completion percentage
                            // It can be used for example:
                            // if (progressUpdate.ProgressStatus > ServiceExecStatus.None)
                            // {
                            //     var completion = progressUpdate.PercentageCompletion > 100 ? 100: progressUpdate.PercentageCompletion;
                            //     _out.Info($"{progressUpdate.ProgressStatus.ToString()} Completion: {completion} %");
                            // }

                            // TODO: Make the completion percentage deterministic
                            if (progressUpdate.ShouldPrintMessage)
                            {
                                consoleOutput.Info(progressUpdate.ProgressMessage.Message, progressUpdate.ProgressMessage.NewLine);
                            }
                        });
                    })
                    .As<IProgress<ProgressUpdate>>()
                    .IfNotRegistered(typeof(IProgress<ProgressUpdate>))
                    .SingleInstance();
        }
    }
}