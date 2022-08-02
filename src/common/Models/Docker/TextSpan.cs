// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.IO;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Common.Models.Docker
{
    /// <summary>
    /// TextSpan represents a portion of the text, excluding line ending character.
    /// </summary>
    internal class TextSpan
    {
        public TextSpan(DockerfileParser parser, int startIndex, int length)
        {
            _parser = parser;
            this.StartIndex = startIndex;
            this.Length = length;
        }

        public int StartIndex { get; private set; }
        public int Length { get; private set; }
        public int EndIndex { get { return this.StartIndex + this.Length; } }

        public void Shift(int delta)
        {
            this.StartIndex = this.StartIndex + delta;
        }

        public string GetContent()
        {
            StringBuilder result = new StringBuilder();
            string rawContent = _parser.GetRawContent(this.StartIndex, this.Length);
            using (StringReader sr = new StringReader(rawContent))
            {
                while (true)
                {
                    string line = sr.ReadLine();
                    if (line == null)
                    {
                        break;
                    }
                    line = line.TrimEnd(' ', '\\', '\t');
                    line = line.TrimStart(' ', '\t');
                    if (result.Length > 0)
                    {
                        result.Append(' ');
                    }
                    result.Append(line);
                }
            }
            return result.ToString();
        }

        private DockerfileParser _parser;
    }
}