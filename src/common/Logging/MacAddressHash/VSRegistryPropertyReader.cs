// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Security;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.Win32;

namespace Microsoft.BridgeToKubernetes.Common.Logging.MacAddressHash
{
    /// <summary>
    /// Stores a property bag in a registry.
    /// Different exe names do not override each other properties.
    /// </summary>
    internal class VSRegistryPropertyReader : IVSRegistryPropertyReader
    {
        private const string KeyPath = @"Software\Microsoft\VisualStudio\Telemetry\PersistentPropertyBag\";
        private static readonly string fullKeyName = @"HKEY_CURRENT_USER\" + KeyPath;
        private const string StringPrefix = "s:";

        private readonly IPlatform _platform;

        public VSRegistryPropertyReader(IPlatform platform)
        {
            this._platform = platform;
        }

        public object GetProperty(string propertyName)
        {
            object result = null;
            if (this._platform.IsWindows)
            {
                SafeRegistryCall(() =>
                {
                    result = InterpretRegistryValue(Registry.GetValue(fullKeyName, propertyName, null));
                });
            }

            return result;
        }

        private static object InterpretRegistryValue(object value)
        {
            var valueAsString = value as string;
            if (valueAsString != null)
            {
                if (valueAsString.StartsWith(StringPrefix))
                {
                    return valueAsString.Substring(StringPrefix.Length);
                }
            }

            return value;
        }

        /// <summary>
        /// Executes an action that manipulates registry and catches safe exceptions.
        /// A safe exception can be a result of the registry key been deleted by another process
        /// or if a user has manipulated with registry to restrict permissions.
        /// </summary>
        /// <param name="action"></param>
        /// <returns>True is the action was executed and false if the action has thrown an safe registry exception.</returns>
        private static bool SafeRegistryCall(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is SecurityException || e is UnauthorizedAccessException || e is InvalidOperationException))
                {
                    throw;
                }

                return false;
            }
        }
    }
}