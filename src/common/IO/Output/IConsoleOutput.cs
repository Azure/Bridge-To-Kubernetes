// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.IO.Output
{
    internal interface IConsoleOutput
    {
        LoggingVerbosity Verbosity { get; }

        OutputFormat OutputFormat { get; set; }

        void Exception(Exception ex);

        void Error(string text, bool newLine = true);

        void Warning(string text, bool newLine = true);

        void Info(string text, bool newLine = true);

        void Verbose(string text, bool newLine = true);

        void Write(EventLevel level, string text, bool newLine = true);

        void Data<T>(IEnumerable<T> data);

        void Flush();

        void WriteRequestId(IOperationIds operationResponse);
    }
}