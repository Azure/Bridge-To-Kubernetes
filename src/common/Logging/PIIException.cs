// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.BridgeToKubernetes.Common.Json;
using System;
using System.Globalization;
using System.Linq;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    /// <summary>
    /// Exception that may contain personally identifiable information
    /// </summary>
    public abstract class PIIException : Exception
    {
        /// <summary>
        /// Format string to compose the exception message
        /// </summary>
        public string Format { get; }

        /// <summary>
        /// Arguments for the format string for the exception message
        /// </summary>
        public object[] Args { get; }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="format">Format string to compose the exception message</param>
        /// <param name="args">Arguments for the format string for the exception message</param>
        internal PIIException(string format, params object[] args) :
            base(_SafeStringFormat(format, args))
        {
            this.Format = format;
            this.Args = args;
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="inner">Inner exception</param>
        /// <param name="format">Format string to compose the exception message</param>
        /// <param name="args">Arguments for the format string for the exception message</param>
        internal PIIException(Exception inner, string format, params object[] args) :
            base(_SafeStringFormat(format, args), inner)
        {
            this.Format = format;
            this.Args = args;
        }

        /// <summary>
        /// Returns a new Exception of the same type with the provided finalized message
        /// </summary>
        /// <param name="message">The final message</param>
        internal abstract PIIException CloneWithFinalMessage(string message);

        /// <summary>
        /// Returns a new Exception of the same type with the provided finalized message
        /// </summary>
        /// <param name="message">The final message</param>
        /// <param name="innerEx">Inner exception</param>
        internal abstract PIIException CloneWithFinalMessage(string message, Exception innerEx);

        private static string _SafeStringFormat(string format, params object[] args)
        {
            if (args == null || !args.Any())
            {
                return format;
            }

            try
            {
                var serializedArgs = args.Select(arg => JsonHelpers.SerializeForLoggingPurpose(arg)).ToArray();
                return string.Format(CultureInfo.InvariantCulture, format, serializedArgs);
            }
            catch (FormatException)
            {
                return $"{format} -- {args.Aggregate((x, y) => $"{x ?? "null"} -- {y ?? "null"}")}";
            }
        }
    }
}