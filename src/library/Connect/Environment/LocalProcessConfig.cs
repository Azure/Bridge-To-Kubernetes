// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using Version = System.Version;

namespace Microsoft.BridgeToKubernetes.Library.Connect.Environment
{
    internal class LocalProcessConfig : ILocalProcessConfig
    {
        internal static readonly Version LatestSupportedVersion = new Version("0.1");
        internal static List<string> EnableFeaturesList = new List<string>();

        private readonly List<EnvironmentEntryIssue> _issues = new List<EnvironmentEntryIssue>();
        private readonly List<EnvironmentEntry> _envVarEntries = new List<EnvironmentEntry>();
        private readonly List<IServiceToken> _serviceTokens = new List<IServiceToken>();
        private readonly List<IVolumeToken> _volumeTokens = new List<IVolumeToken>();
        private readonly List<IExternalEndpointToken> _externalEndpointTokens = new List<IExternalEndpointToken>();

        public delegate ILocalProcessConfig Factory(string filePath);

        /// <summary>
        /// Constructor
        /// </summary>
        public LocalProcessConfig(
            string filePath,
            IFileSystem fileSystem,
            ILog log)
        {
            foreach (var value in Enum.GetNames(typeof(EnableFeature)))
            {
                EnableFeaturesList.Add(value);
            }

            try
            {
                this.ConfigFilePath = filePath;
                string content;
                LocalProcessConfigFile config;

                // Read file
                try
                {
                    content = fileSystem.ReadAllTextFromFile(filePath);
                    if (string.IsNullOrEmpty(content))
                    {
                        _issues.Add(new EnvironmentEntryIssue()
                        {
                            IssueType = EnvironmentEntryIssueType.Error,
                            Message = $"Failed to read {Product.Name} config file '{filePath}': the file is empty"
                        });
                        return;
                    }
                }
                catch (Exception e)
                {
                    _issues.Add(new EnvironmentEntryIssue()
                    {
                        IssueType = EnvironmentEntryIssueType.Error,
                        Message = $"Failed to read {Product.Name} config file '{filePath}': {e.Message}"
                    });
                    return;
                }

                // Parse file
                try
                {
                    config = new Deserializer().Deserialize<LocalProcessConfigFile>(content);
                }
                catch (YamlException e)
                {
                    _issues.Add(new EnvironmentEntryIssue()
                    {
                        IssueType = EnvironmentEntryIssueType.Error,
                        Message = $"Failed to parse {Product.Name} config file '{filePath}': {e.Message}"
                    });
                    return;
                }

                AssertHelper.NotNull(config, nameof(config));
                if (config.Version == null)
                {
                    _issues.Add(new EnvironmentEntryIssue()
                    {
                        IssueType = EnvironmentEntryIssueType.Error,
                        Message = $"{Product.Name} config file '{filePath}' does not specify a version"
                    });
                    return;
                }
                else if (config.Version > LatestSupportedVersion)
                {
                    _issues.Add(new EnvironmentEntryIssue()
                    {
                        IssueType = EnvironmentEntryIssueType.Error,
                        Message = $"{Product.Name} config file '{filePath}' is using an unsupported version. Latest supported version: {LatestSupportedVersion}"
                    });
                    return;
                }

                if (config.EnableFeatures != null)
                {
                    foreach (var feature in config.EnableFeatures)
                    {
                        if (!EnableFeaturesList.Contains(feature))
                        {
                            _issues.Add(new EnvironmentEntryIssue()
                            {
                                IssueType = EnvironmentEntryIssueType.Warning,
                                Message = $"Feature is not supported: '{feature}'"
                            });
                            continue;
                        }

                        if (StringComparer.OrdinalIgnoreCase.Equals(feature, EnableFeature.ManagedIdentity.ToString()))
                        {
                            this.IsManagedIdentityScenario = true;
                            if (config.EnvironmentVariables == null)
                            {
                                config.EnvironmentVariables = new List<LocalProcessConfigFile_EnvVar>();
                            }

                            config.EnvironmentVariables.Add(
                                new LocalProcessConfigFile_EnvVar()
                                {
                                    Name = ManagedIdentity.MSI_ENDPOINT_EnvironmentVariable,
                                    Value = ManagedIdentity.EndpointValue
                                });

                            config.EnvironmentVariables.Add(
                                new LocalProcessConfigFile_EnvVar()
                                {
                                    Name = ManagedIdentity.MSI_SECRET_EnvironmentVariable,
                                    Value = ManagedIdentity.SecretValue
                                });
                        }

                        if (StringComparer.OrdinalIgnoreCase.Equals(feature, EnableFeature.Probes.ToString()))
                        {
                            this.IsProbesEnabled = true;
                        }

                        if (StringComparer.OrdinalIgnoreCase.Equals(feature, EnableFeature.LifecycleHooks.ToString()))
                        {
                            this.IsLifecycleHooksEnabled = true;
                        }
                    }
                }

                if (config.EnvironmentVariables != null)
                {
                    foreach (var env in config.EnvironmentVariables)
                    {
                        env.Name = env.Name ?? string.Empty;
                        env.Value = env.Value ?? string.Empty;
                        try
                        {
                            var entry = new EnvironmentEntry(env.Name, env.Value, config);
                            if (entry != null)
                            {
                                foreach (var t in entry.Tokens)
                                {
                                    // TODO: t.Serialize() returns "Serialization Error" when port is specified. Need to fix this at some point, but it doesn't affect the user experience at all.
                                    log.Verbose("Loaded env var '{0}' of type {1}: {2} => {3}", new PII(t.Name), t.GetType().Name, new PII(JsonHelpers.SerializeForLoggingPurpose(t)), new PII(t.Evaluate()));
                                }
                                _envVarEntries.Add(entry);
                                _serviceTokens.AddRange(entry.Tokens.OfType<IServiceToken>().Distinct().Except(_serviceTokens));
                                _volumeTokens.AddRange(entry.Tokens.OfType<IVolumeToken>().Distinct().Except(_volumeTokens));
                                _externalEndpointTokens.AddRange(entry.Tokens.OfType<IExternalEndpointToken>().Distinct().Except(_externalEndpointTokens));
                            }
                        }
                        catch (NotImplementedException notImplemetedEx)
                        {
                            _issues.Add(new EnvironmentEntryIssue()
                            {
                                IssueType = EnvironmentEntryIssueType.Warning,
                                Message = $"Content is not supported: name:'{env.Name}',value:'{env.Value}'. {notImplemetedEx.Message}"
                            });
                        }
                        catch (Exception ex)
                        {
                            _issues.Add(new EnvironmentEntryIssue()
                            {
                                IssueType = EnvironmentEntryIssueType.Error,
                                Message = $"Exception parsing content: name:'{env.Name}',value:'{env.Value}'. {ex.Message}"
                            });
                        }
                    }
                }
            }
            finally
            {
                if (_issues != null && _issues.Any())
                {
                    log.Warning("{0} config parse issues encountered with file '{1}': {2}", Product.Name, new PII(filePath), new PII(JsonHelpers.SerializeForLoggingPurpose(_issues)));
                }
            }
        }

        /// <summary>
        /// <see cref="ILocalProcessConfig.ConfigFilePath"/>
        /// </summary>
        public string ConfigFilePath { get; }

        /// <summary>
        /// <see cref="ILocalProcessConfig.AllIssues"/>
        /// </summary>
        public IEnumerable<EnvironmentEntryIssue> AllIssues => _issues;

        /// <summary>
        /// <see cref="ILocalProcessConfig.ErrorIssues"/>
        /// </summary>
        public IEnumerable<EnvironmentEntryIssue> ErrorIssues => _issues.Where(i => i.IssueType == EnvironmentEntryIssueType.Error);

        /// <summary>
        /// <see cref="ILocalProcessConfig.IsSuccess"/>
        /// </summary>
        public bool IsSuccess => !ErrorIssues.Any();

        /// <summary>
        /// <see cref="ILocalProcessConfig.ReferencedServices"/>
        /// </summary>
        public IEnumerable<IServiceToken> ReferencedServices => _serviceTokens;

        /// <summary>
        /// <see cref="ILocalProcessConfig.ReferencedVolumes"/>
        /// </summary>
        public IEnumerable<IVolumeToken> ReferencedVolumes => _volumeTokens;

        /// <summary>
        /// <see cref="ILocalProcessConfig.ReferencedExternalEndpoints"/>
        /// </summary>
        public IEnumerable<IExternalEndpointToken> ReferencedExternalEndpoints => _externalEndpointTokens;

        /// <summary>
        /// <see cref="ILocalProcessConfig.IsManagedIdentityScenario"/>
        /// </summary>
        public bool IsManagedIdentityScenario { get; } = false;

        /// <summary>
        /// <see cref="ILocalProcessConfig.IsProbesEnabled"/>
        /// </summary>
        public bool IsProbesEnabled { get; } = false;

        /// <summary>
        /// <see cref="ILocalProcessConfig.IsLifecycleHooksEnabled"/>
        /// </summary>
        public bool IsLifecycleHooksEnabled { get; } = false;

        /// <summary>
        /// <see cref="ILocalProcessConfig.EvaluateEnvVars"/>
        /// </summary>
        public IDictionary<string, string> EvaluateEnvVars()
        {
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (var entry in _envVarEntries)
            {
                result[entry.Name] = entry.Evaluate();
            }
            return result;
        }
    }
}