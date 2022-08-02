// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.BridgeToKubernetes.Exe.Commands
{
    internal class CliCommandOption : CommandOption
    {
        public CliCommandOption(string option, string description, CommandOptionType type, bool isRequired, bool showInHelpText = false)
            : base(option, type)
        {
            this.IsRequired = isRequired;
            this.Description = description;

            if (isRequired)
            {
                this.Description += " [Required]";
            }

            this.ShowInHelpText = showInHelpText;
        }

        public CliCommandOption(string shortOption, string longOption, string description, CommandOptionType type, bool isRequired, bool showInHelpText = false)
            : this($"{shortOption}|{longOption}", description, type, isRequired, showInHelpText) { }

        /// <summary>
        /// Gets the value indicating if command option is required or not.
        /// </summary>
        public bool IsRequired
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the option value or null if value is not set.
        /// </summary>
        /// <exception cref="ArgumentNullException">If a required value is not provided</exception>
        public string GetValue()
        {
            var optionValue = this.HasValue() ? this.Value() : null;
            if (optionValue == null && this.IsRequired)
            {
                throw new ArgumentNullException(this.LongName);
            }
            return optionValue;
        }
    }
}