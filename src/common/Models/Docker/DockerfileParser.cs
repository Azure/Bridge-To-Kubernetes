// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Common.Models.Docker
{
    /// <summary>
    /// DockerfileParser is a simple Dockerfile parser. Usage:
    ///
    ///            DockerfileParser parser = new DockerfileParser(File.ReadAllText(f));
    ///            foreach(var command in parser.Commands)
    ///            {
    ///                Console.WriteLine($"{command.Instruction} -- {command.Arguments}**END**");
    ///            }
    ///            parser.Remove("0");
    ///            parser.Insert(0, "FROM myimage");
    ///            parser.Add("EXPOSE 5000");
    ///            StringBuilder buffer = new StringBuilder();
    ///            using (StringWriter sw = new StringWriter(buffer))
    ///            {
    ///                parser.Save(sw);
    ///            }
    /// </summary>
    internal class DockerfileParser
    {
        public DockerfileParser(string content)
        {
            _buffer = new StringBuilder(content);
            this.Parse();
        }

        public DockerfileParser()
        {
            _buffer = new StringBuilder();
            this.Parse();
        }

        public ReadOnlyCollection<DockerCommand> Commands
        {
            get { return _commands.AsReadOnly(); }
        }

        public void Save(TextWriter writer)
        {
            writer.Write(_buffer.ToString());
        }

        public void Insert(int index, string command)
        {
            if (index < 0)
            {
                throw new IndexOutOfRangeException();
            }

            if (index >= _commands.Count)
            {
                Add(command);
            }

            int delta = 0;
            _buffer.Insert(_commands[index].Span.StartIndex, '\n');
            delta++;
            if (!_isUnixLineEnding)
            {
                _buffer.Insert(_commands[index].Span.StartIndex, '\r');
                delta++;
            }
            _buffer.Insert(_commands[index].Span.StartIndex, command);
            delta += command.Length;

            TextSpan s = new TextSpan(this, _commands[index].Span.StartIndex, command.Length);
            for (int i = index; i < _commands.Count; i++)
            {
                _commands[i].Span.Shift(delta);
            }
            _commands.Insert(index, new DockerCommand(s));
        }

        public void Remove(int index)
        {
            if (index < 0 || index >= _commands.Count)
            {
                throw new IndexOutOfRangeException();
            }

            int removeLength;
            DockerCommand itemToRemove = _commands[index];
            if (index + 1 == _commands.Count)
            {
                removeLength = _buffer.Length - itemToRemove.Span.StartIndex;
                _buffer.Remove(itemToRemove.Span.StartIndex, removeLength);
            }
            else
            {
                removeLength = _commands[index + 1].Span.StartIndex - itemToRemove.Span.StartIndex;
                _buffer.Remove(itemToRemove.Span.StartIndex, removeLength);
            }
            _commands.RemoveAt(index);
            for (int i = index; i < _commands.Count; i++)
            {
                _commands[i].Span.Shift(-removeLength);
            }
        }

        public void Add(string command)
        {
            if (!_isUnixLineEnding)
            {
                _buffer.Append('\r');
            }
            _buffer.Append('\n');
            int startIndex = _buffer.Length;
            _buffer.Append(command);
            int length = _buffer.Length - startIndex;

            TextSpan s = new TextSpan(this, startIndex, length);
            DockerCommand di = new DockerCommand(s);
            _commands.Add(di);
        }

        public string GetRawContent(int startIndex, int length)
        {
            return _buffer.ToString(startIndex, length);
        }

        private void Parse()
        {
            List<DockerCommand> commands = new List<DockerCommand>();
            int i = 0;
            while (_buffer.Length > i)
            {
                i = this.SkipLineEndings(i);
                int line = this.ReadLine(i);
                if (line == 0)
                {
                    i++;
                    continue;
                }
                TextSpan span = new TextSpan(this, i, line);
                i += line;
                string content = span.GetContent();
                if (!string.IsNullOrWhiteSpace(content))
                {
                    commands.Add(new DockerCommand(span));
                }
            }
            _commands = commands;
        }

        private int SkipLineEndings(int index)
        {
            while (_buffer.Length > index && (_buffer[index] == '\n' || _buffer[index] == '\r'))
            {
                index++;
            }
            return index;
        }

        private int ReadLine(int index)
        {
            int i = index;
            int currentLineStart = index;
            while (_buffer.Length > i)
            {
                if (_buffer[i] != '\n')
                {
                    i++;
                    continue;
                }
                int lineEnds = i;
                while (i >= currentLineStart && (_buffer[i] == '\r' || _buffer[i] == '\n' || _buffer[i] == ' ' || _buffer[i] == '\t'))
                {
                    i--;
                }

                if (_buffer[i] == '\\')
                {
                    i = lineEnds + 1;
                    currentLineStart = i;
                    continue;
                }
                else
                {
                    return i - index + 1;
                }
            }
            return i - index;
        }

        private StringBuilder _buffer = null;
        private bool _isUnixLineEnding = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private List<DockerCommand> _commands = null;
    }
}