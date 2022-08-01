// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal abstract class EnvironmentTokenBase : IEnvironmentToken
    {
        protected string _tokenStr; // the original string to be replaced

        public EnvironmentTokenBase(string name, string tokenString)
        {
            this.Name = name;
            this._tokenStr = tokenString;
        }

        /// <summary>
        /// <see cref="IEnvironmentToken.Name"/>
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// <see cref="IEnvironmentToken.Evaluate"/>
        /// </summary>
        /// <returns></returns>
        public abstract string Evaluate();
    }
}