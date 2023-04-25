// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using FakeItEasy;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Xunit;

namespace Microsoft.BridgeToKubernetes.Common.Tests
{
    public class ConsoleOutputTests
    {
        #region Complex Output Data

        [Fact]
        public void NoItems()
        {
            var fakeConsole = A.Fake<IConsole>();
            ConsoleOutput consoleOutput = new ConsoleOutput(A.Fake<ILog>(), fakeConsole, A.Fake<CommandLineArgumentsManager>());

            var testData = new List<TestOutputData>();
            consoleOutput.Data(testData);
            consoleOutput.OutputFormat = OutputFormat.Json;
            consoleOutput.Data(testData);

            const string ExpectedRow = "Name1Override  Name2  Name3TableOverride";
            const string ExpectedContextJson = "[]";
            A.CallTo(() => fakeConsole.WriteLine(ExpectedRow)).MustHaveHappened();
            A.CallTo(() => fakeConsole.Write(ExpectedContextJson)).MustHaveHappened();
        }

        [Fact]
        public void SingleItem()
        {
            var fakeConsole = A.Fake<IConsole>();
            ConsoleOutput consoleOutput = new ConsoleOutput(A.Fake<ILog>(), fakeConsole, A.Fake<CommandLineArgumentsManager>());

            var testData = new List<TestOutputData>()
            {
                new TestOutputData(){Name1="name1", Name2="Name2", Name3="name3" }
            };

            consoleOutput.Data(testData);
            consoleOutput.OutputFormat = OutputFormat.Json;
            consoleOutput.Data(testData);

            const string ExpectedHeaderRow = "Name1Override  Name2  Name3TableOverride";
            const string ExpectedTestRow = "name1          Name2  name3";
            const string ExpectedTestItemJson = @"[
              {
                ""name1"": ""name1"",
                ""Name2Override"": ""Name2"",
                ""Name3JsonOverride"": ""name3""
              }
            ]";
            A.CallTo(() => fakeConsole.WriteLine(ExpectedHeaderRow)).MustHaveHappened();
            A.CallTo(() => fakeConsole.WriteLine(ExpectedTestRow)).MustHaveHappened();
            A.CallTo(() => fakeConsole.Write(A<string>.That.Matches(
                s => StringComparer.Ordinal.Equals(Regex.Replace(s, @"\s", ""), (Regex.Replace(ExpectedTestItemJson, @"\s", "")))))).MustHaveHappened();
        }

        [Fact]
        public void SingleItemWithNull()
        {
            var fakeConsole = A.Fake<IConsole>();
            ConsoleOutput consoleOutput = new ConsoleOutput(A.Fake<ILog>(), fakeConsole, A.Fake<CommandLineArgumentsManager>());

            var testData = new List<TestOutputData>()
            {
                new TestOutputData(){Name1=null, Name2=null, Name3="name3" }
            };

            consoleOutput.Data(testData);
            consoleOutput.OutputFormat = OutputFormat.Json;
            consoleOutput.Data(testData);

            const string ExpectedHeaderRow = "Name1Override  Name2  Name3TableOverride";
            const string ExpectedTestRow = "                      name3";
            const string ExpectedTestItemJson = @"[
              {
                ""name1"": null,
                ""Name2Override"": null,
                ""Name3JsonOverride"": ""name3""
              }
            ]";
            A.CallTo(() => fakeConsole.WriteLine(ExpectedHeaderRow)).MustHaveHappened();
            A.CallTo(() => fakeConsole.WriteLine(ExpectedTestRow)).MustHaveHappened();
            A.CallTo(() => fakeConsole.Write(A<string>.That.Matches(
                s => StringComparer.Ordinal.Equals(Regex.Replace(s, @"\s", ""), (Regex.Replace(ExpectedTestItemJson, @"\s", "")))))).MustHaveHappened();
        }

        private class TestOutputData
        {
            [DisplayName("Name1Override")]
            public string Name1 { get; set; }

            [JsonPropertyName("Name2Override")]
            public string Name2 { get; set; }

            [DisplayName("Name3TableOverride")]
            [JsonPropertyName("Name3JsonOverride")]
            public string Name3 { get; set; }
        }

        #endregion Complex Output Data
    }
}