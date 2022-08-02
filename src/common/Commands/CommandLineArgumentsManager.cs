// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Commands
{
    /// <summary>
    /// This class is in charge of parsing global options i.e. verbosity, and splitting out CommandExecuteArgs
    /// </summary>
    internal class CommandLineArgumentsManager
    {
        public ICommand Command { get; set; }

        public List<string> Arguments { get; set; }

        /// <summary>
        /// An array of additional arguments that are passed through by the command
        /// </summary>
        internal string[] CommandExecuteArguments { get; private set; }

        public LoggingVerbosity Verbosity { get; private set; } = LoggingVerbosity.Normal;

        public OutputFormat OutputFormat { get; private set; } = OutputFormat.Table;

        public void ParseGlobalArgs(string[] args)
        {
            if (this.Arguments == null)
            {
                this.Arguments = new List<string>();
            }

            // Read the Verbosity and Output arguments and add the remaining arguments to Arguments.
            for (int index = 0; index < args.Length; index++)
            {
                if (StringComparer.Ordinal.Equals(args[index], Constants.CommandOptions.Verbose.Option))
                {
                    this.Verbosity = LoggingVerbosity.Verbose;
                }
                else if (StringComparer.Ordinal.Equals(args[index], Constants.CommandOptions.Quiet.Option))
                {
                    this.Verbosity = LoggingVerbosity.Quiet;
                }
                else if (StringComparer.Ordinal.Equals(args[index], Constants.CommandOptions.OutputType.Option))
                {
                    if (index == args.Length - 1
                        || !Enum.TryParse(args[++index], ignoreCase: true, result: out OutputFormat output))
                    {
                        throw new ArgumentException($"Unrecognized {Constants.CommandOptions.OutputType.Option} type '{args[index]}'");
                    }
                    this.OutputFormat = output;
                }
                else
                {
                    Arguments.Add(args[index]);
                }
            }

            Arguments = FilterCommandExecuteArguments(Arguments);
            Debug.Assert(!this.Arguments.Contains("--"), "We should not pass -- as one of the arguments to Execute command otherwise it will fail. Please make sure we use the input args in this method and not the global Arguments since we have special-parsed global Arguments to create input args");
        }

        /// <summary>
        /// To support '--' option. Arguments passed after '--' are directly sent as part of the command.
        /// </summary>
        private List<string> FilterCommandExecuteArguments(List<string> args)
        {
            if (args != null)
            {
                for (int i = 0; i < args.Count; i++)
                {
                    if (args[i] == "--" && i + 1 < args.Count)
                    {
                        CommandExecuteArguments = args.Skip(i + 1).ToArray();
                        return args.Take(i).ToList();
                    }
                }
            }
            CommandExecuteArguments = new string[] { };
            return args;
        }
    }
}