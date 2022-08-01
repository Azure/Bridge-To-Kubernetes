// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal class EnvironmentEntryIssue
    {
        public string Message { get; set; }
        public EnvironmentEntryIssueType IssueType { get; set; }
    }
}