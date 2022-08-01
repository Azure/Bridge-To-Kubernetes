// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Common.Utilities
{
    /// <summary>
    /// A string buffer of a fixed maximum total length. After reaching full capacity strings are dropped FIFO.
    /// </summary>
    internal class FixedSizeStringBuilder
    {
        private readonly Queue<string> q = new Queue<string>();
        public int MaxLength { get; } = 0;
        public int Length { get; private set; } = 0;
        public bool MaxLengthReached { get; private set; } = false;

        /// <summary>
        /// Creates a string buffer of a fixed maximum length. After reaching full capacity strings are dropped FIFO.
        /// </summary>
        /// <param name="maxLength">Maximum number of characters the buffer can hold</param>
        public FixedSizeStringBuilder(int maxLength)
        {
            this.MaxLength = maxLength;
        }

        /// <summary>
        /// Appends the default line terminator to the end of the current FixedSizeStringBuilder object.
        /// </summary>
        public void AppendLine()
            => this.Append(Environment.NewLine);

        /// <summary>
        /// Appends a copy of the specified string followed by the default line terminator to the end of the current FixedSizeStringBuilder object.
        /// </summary>
        public void AppendLine(string value)
            => this.Append(value + Environment.NewLine);

        /// <summary>
        /// Appends a copy of the specified string to this instance.
        /// </summary>
        /// <param name="value"></param>
        public void Append(string value)
        {
            if (value.Length > MaxLength)
            {
                MaxLengthReached = true;
                value = value.Substring(value.Length - MaxLength);
                q.Clear();
                Length = 0;
            }

            q.Enqueue(value);
            Length += value.Length;

            if (Length > MaxLength)
            {
                MaxLengthReached = true;

                while (Length > MaxLength)
                {
                    Length -= q.Dequeue().Length;
                }
            }
        }

        /// <summary>
        /// Prepends '...' if <see cref="MaxLengthReached"/> to indicate data has been dropped
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            if (Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (var e in q)
            {
                sb.Append(e);
            }

            return MaxLengthReached ? $"...{sb.ToString()}" : sb.ToString();
        }
    }
}