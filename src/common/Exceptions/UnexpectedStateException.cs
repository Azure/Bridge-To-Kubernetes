// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Exceptions
{
    /// <summary>
    /// Exception class intended to be used for truly "unexpected" situations. This class should NEVER inherit from <see cref="IUserVisibleExceptionReporter"/>.
    /// </summary>
    internal class UnexpectedStateException : Exception
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"/>
        /// <param name="log">Constructor will log a Critical message</param>
        internal UnexpectedStateException(string message, ILog log)
            : base(message)
        {
            log?.Critical($"{nameof(UnexpectedStateException)} - {message}");
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="message"/>
        /// <param name="inner"/>
        /// <param name="log">Constructor will log a Critical message</param>
        internal UnexpectedStateException(string message, Exception inner, ILog log)
            : base(message, inner)
        {
            log?.Critical($"{nameof(UnexpectedStateException)} - {message} >> {inner?.Message}");
        }
    }
}