// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;

namespace Microsoft.BridgeToKubernetes.Common.Models.Docker
{
    internal class DockerCommand
    {
        public DockerCommand(TextSpan span)
        {
            this.Span = span;
            this.Parse();
        }

        public TextSpan Span { get; private set; }

        public DockerInstruction Instruction { get; private set; }

        public string Arguments { get; private set; }

        private void Parse()
        {
            string content = this.Span.GetContent().Trim();
            if (string.IsNullOrEmpty(content))
            {
                this.Instruction = DockerInstruction.NONE;
                this.Arguments = string.Empty;
            }
            else if (content.StartsWith("#", StringComparison.Ordinal))
            {
                this.Instruction = DockerInstruction.COMMENT;
                this.Arguments = content;
            }
            else
            {
                int instructionLen = -1;
                for (int i = 0; i < content.Length; i++)
                {
                    if ((content[i] >= 'A' && content[i] <= 'Z') || (content[i] >= 'a' && content[i] <= 'z'))
                    {
                        continue;
                    }
                    else
                    {
                        instructionLen = i;
                        break;
                    }
                }
                if (instructionLen < 0)
                {
                    this.Instruction = DockerInstruction.NONE;
                    this.Arguments = content;
                }
                else
                {
                    this.Instruction = GetDockerInstruction(content.Substring(0, instructionLen));
                    this.Arguments = content.Substring(instructionLen).Trim();
                }
            }
        }

        private static DockerInstruction GetDockerInstruction(string instruction)
        {
            DockerInstruction i = DockerInstruction.NONE;
            instruction = instruction.Trim().ToUpperInvariant();
            Enum.TryParse<DockerInstruction>(instruction, out i);
            return i;
        }
    }
}