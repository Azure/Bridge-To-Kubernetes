// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Exe.Commands;

namespace Microsoft.BridgeToKubernetes.Exe.Telemetry
{
    internal class CommandLogging
    {
        private Stopwatch _sw = Stopwatch.StartNew();

        private string[] _args;
        private ILog _log;

        public CommandLogging(string[] args, ILog log)
        {
            this._args = args;
            this._log = log;

            if (!UserAllowsLogging())
            {
                return;
            }

            this._log?.Event(
                "Command.Start",
                new Dictionary<string, object> { { "Arguments", GetArgsAsPIIAsRequired(args) }, { LoggingConstants.Property.IsRoutingEnabled, args?.Contains(CommandConstants.Options.Routing.Option) } });
        }

        public void Finished(bool success, string failureReason)
        {
            this._sw.Stop();

            if (!UserAllowsLogging())
            {
                return;
            }

            var properties = new Dictionary<string, object>
            {
                { "Arguments", GetArgsAsPIIAsRequired(this._args) },
                { "Result", (success ? "Succeeded" : "Failed") }
            };

            if (!string.IsNullOrWhiteSpace(failureReason))
            {
                // Ensure no secrets are logged
                properties.Add("FailureReason2", StringManipulation.RemovePrivateKeyIfNeeded(failureReason));
            }

            this._log?.Event(
                "Command.End",
                properties,
                new Dictionary<string, double> { { "Duration", this._sw.ElapsedMilliseconds } });
        }

        private static object[] GetArgsAsPIIAsRequired(string[] args)
        {
            var whiteList = GetConstantsValues(typeof(CommandConstants)).Select(cmd => cmd.ToLowerInvariant()).ToHashSet();
            var lowerCaseArgs = args.Select(a => a.ToLowerInvariant());
            return lowerCaseArgs.Select(a => whiteList.Contains(a) ? a : (object)new PII(a)).ToArray();
        }

        /// <summary>
        /// Return all the values of the constants in a Type. It navigates recursively the type to support nested constants
        /// </summary>
        /// <param name="t">The type to read the constants from</param>
        /// <returns></returns>
        private static List<string> GetConstantsValues(Type t)
        {
            var constants = t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                .Where(fi => fi.IsLiteral && !fi.IsInitOnly)
                                .Select(c => c.GetValue(null).ToString())
                                .ToList();
            var subclasses = t.GetNestedTypes(BindingFlags.Public | BindingFlags.Static).ToList();
            if (subclasses.Any())
            {
                subclasses.ForEach(sc => constants.AddRange(GetConstantsValues(sc)));
            }
            return constants;
        }

        /// <summary>
        /// Indicates if the user allows logging
        /// </summary>
        /// <returns>
        /// Right now is hardcoded to TRUE, need to be tied to some setting file to be persisted locally to support opt-in/opt-out.
        /// </returns>
        private static bool UserAllowsLogging()
        {
            return true;
        }
    }
}