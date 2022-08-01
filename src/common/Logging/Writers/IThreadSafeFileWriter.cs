// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal interface IThreadSafeFileWriter : IDisposable
    {
        /// <summary>
        /// Gets the fully-qualified path for the current file
        /// </summary>
        string CurrentFilePath { get; }

        Task WriteLineAsync(string line);

        Task FlushAsync();
    }
}