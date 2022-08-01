// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    /// <summary>
    /// Token to be copied over verbatim without replacement e.g. "my/own/path/here"
    /// </summary>
    internal class VerbatimToken : EnvironmentTokenBase
    {
        public VerbatimToken(string tokenString) : base(name: string.Empty, tokenString: tokenString)
        {
        }

        public override string Evaluate()
        {
            return this._tokenStr;
        }
    }
}