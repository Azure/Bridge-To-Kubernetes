// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Utilities;

namespace Microsoft.BridgeToKubernetes.Common.Logging
{
    internal class ThreadSafeFileWriter : IThreadSafeFileWriter
    {
        private const int _maxBufferSizeBytes = 10000;
        private volatile Lazy<TextWriter> _textWriter;
        private volatile string _currentFilePath;
        private readonly Timer _updateFileWriterTimer;
        private readonly Timer _flushFileWriterTimer;

        public ThreadSafeFileWriter(string filePath)
        {
            _textWriter = new Lazy<TextWriter>(() => CreateTextWriter(filePath));
        }

        public ThreadSafeFileWriter(string basePath, TimeSpan interval)
        {
            // We create the first one here to avoid race condition between the FileLogger Flush and the creation of the FileWriter with the Timer.
            this.CreateIntervalFile(basePath, interval);
            this._updateFileWriterTimer = new Timer(state => this.CreateIntervalFile(basePath, interval), null, interval, interval);
            this._flushFileWriterTimer = new Timer(async (state) => await this.FlushAsync(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }

        public string CurrentFilePath => _currentFilePath ?? string.Empty;

        public void Dispose()
        {
            this._updateFileWriterTimer?.Dispose();
            this._flushFileWriterTimer?.Dispose();
            if (this._textWriter?.IsValueCreated ?? false)
            {
                this._textWriter.Value.Dispose();
            }
            this._textWriter = null;
        }

        public Task FlushAsync()
            => this._textWriter?.Value?.FlushAsync() ?? Task.CompletedTask;

        public Task WriteLineAsync(string line)
            => this._textWriter?.Value?.WriteLineAsync(line);

        private void CreateIntervalFile(string basePath, TimeSpan interval)
        {
            var oldTextWriter = _textWriter;
            var timeFormat = interval < TimeSpan.FromDays(1) ? "yyyy-MM-dd_HH-mm-ss" : "yyyy-MM-dd";
            _textWriter = new Lazy<TextWriter>(() => CreateTextWriter($"{basePath}-{DateTime.UtcNow.ToString(timeFormat)}.log"));

            // NOTE: AsyncHelpers.RunSync throws if the task target is null
            if (oldTextWriter != null && oldTextWriter.IsValueCreated)
            {
                AsyncHelpers.RunSync(() => oldTextWriter.Value.FlushAsync());
                oldTextWriter.Value.Dispose();
            }
        }

        private TextWriter CreateTextWriter(string filePath)
        {
            TextWriter result = null;
            string finalFilePath = null;
            try
            {
                var directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrWhiteSpace(directoryPath) && !Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // If the File name already exists in the first iteration try successive names in the next iterations.
                for (int i = 1; i < 50; i++)
                {
                    string uniqueFilePath = filePath;
                    if (i > 1)
                    {
                        var uniqueFileName = $"{Path.GetFileNameWithoutExtension(filePath)}-{i.ToString()}{Path.GetExtension(filePath)}";
                        uniqueFilePath = Path.Combine(directoryPath, uniqueFileName);
                    }

                    if (File.Exists(uniqueFilePath))
                    {
                        continue;
                    }

                    try
                    {
                        var streamWriter = new StreamWriter(File.Create(uniqueFilePath), System.Text.Encoding.Default, _maxBufferSizeBytes)
                        {
                            AutoFlush = true
                        };
                        result = TextWriter.Synchronized(streamWriter);
                        finalFilePath = uniqueFilePath;
                        // Successfully opened a text writer on a unique file name, so break out of the loop
                        break;
                    }
                    catch (IOException)
                    {
                        // Do nothing, filePath will be updated on next iteration
                        continue;
                    }
                }
            }
            catch { }

            // Set class variable
            _currentFilePath = finalFilePath;
            return result;
        }
    }
}