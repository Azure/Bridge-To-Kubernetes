// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Commands;
using Microsoft.BridgeToKubernetes.Common.Exceptions;
using Microsoft.BridgeToKubernetes.Common.IO.Input;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.ClientFactory;
using Microsoft.BridgeToKubernetes.Library.Models;
using Microsoft.Extensions.CommandLineUtils;

namespace Microsoft.BridgeToKubernetes.Exe.Commands.Connect
{
    /// <summary>
    /// This abstract command is meant to serve as base for all the connect commands that require resolving a target container
    /// given the a combination of parameter given (Deployment, pod, service, containername, kubeconfig, namespace, etc)
    /// </summary>
    internal abstract class TargetConnectCommandBase : CommandBase
    {
        protected string _targetContainer = string.Empty;
        protected string _targetPod = string.Empty;
        protected string _targetDeployment = string.Empty;
        protected string _targetService = string.Empty;
        protected string _targetNamespace;
        protected string _targetKubeConfigContext;

        private CliCommandOption targetNamespaceOption = null;
        private CliCommandOption targetContainerOption = null;
        private CliCommandOption targetDeploymentOption = null;
        private CliCommandOption targetPodOption = null;
        private CliCommandOption targetServiceOption = null;
        private CliCommandOption targetKubeConfigContextOption = null;

        public TargetConnectCommandBase(
            CommandLineArgumentsManager commandLineArgumentsManager,
            IManagementClientFactory clientFactory,
            ILog log,
            IOperationContext operationContext,
            IConsoleInput consoleInput,
            IConsoleOutput consoleOutput,
            IProgress<ProgressUpdate> progress,
            ICliCommandOptionFactory cliCommandOptionFactory,
            ISdkErrorHandling sdkErrorHandling)
            : base(
                  commandLineArgumentsManager,
                  clientFactory,
                  log,
                  operationContext,
                  consoleInput,
                  consoleOutput,
                  progress,
                  cliCommandOptionFactory,
                  sdkErrorHandling)
        { }

        public override void Configure(CommandLineApplication app)
        {
            this.targetNamespaceOption = _cliCommandOptionFactory.CreateConnectTargetNamespaceOption();
            this.targetContainerOption = _cliCommandOptionFactory.CreateConnectTargetContainerOption();
            this.targetDeploymentOption = _cliCommandOptionFactory.CreateConnectWithDeploymentOption();
            this.targetPodOption = _cliCommandOptionFactory.CreateConnectTargetPodOption();
            this.targetServiceOption = _cliCommandOptionFactory.CreateConnectTargetServiceOption();
            this.targetKubeConfigContextOption = _cliCommandOptionFactory.CreateConnectTargetKubeConfigContextOption();

            this._command.Options.Add(targetContainerOption);
            this._command.Options.Add(targetDeploymentOption);
            this._command.Options.Add(targetPodOption);
            this._command.Options.Add(targetServiceOption);
            this._command.Options.Add(targetNamespaceOption);
            this._command.Options.Add(targetKubeConfigContextOption);
        }

        protected void ParseTargetOptions()
        {
            // If multiple container identifiers are present, throw error
            if ((targetDeploymentOption.HasValue() && (targetPodOption.HasValue() || targetServiceOption.HasValue()))
                || (targetPodOption.HasValue() && targetServiceOption.HasValue()))
            {
                throw new InvalidUsageException(_operationContext, Resources.Error_SpecifyOneContainerIdentifier, $"{targetDeploymentOption.Template}, {targetPodOption.Template}, {targetServiceOption.Template}");
            }
            // At least one pod identifier needs to be present.
            if (!targetPodOption.HasValue() && !targetDeploymentOption.HasValue() && !targetServiceOption.HasValue())
            {
                throw new InvalidUsageException(_operationContext, Resources.Error_SpecifyOneContainerIdentifier, $"{targetDeploymentOption.Template}, {targetPodOption.Template}, {targetServiceOption.Template}");
            }
            if (targetContainerOption.HasValue())
            {
                if (!KubernetesUtilities.IsValidK8sObjectName(targetContainerOption.Value()))
                {
                    throw new InvalidUsageException(_operationContext, Resources.Error_InvalidConfigurationValue, targetContainerOption.Template);
                }
                _operationContext.LoggingProperties.Add(CliConstants.Properties.TargetContainerName, new PII(targetContainerOption.Value()));
                _targetContainer = targetContainerOption.Value();
            }
            if (targetPodOption.HasValue())
            {
                if (!KubernetesUtilities.IsValidK8sObjectName(targetPodOption.Value()))
                {
                    throw new InvalidUsageException(_operationContext, Resources.Error_InvalidConfigurationValue, targetPodOption.Template);
                }
                _operationContext.LoggingProperties.Add(CliConstants.Properties.TargetPodName, new PII(targetPodOption.Value()));
                _targetPod = targetPodOption.Value();
            }
            if (targetDeploymentOption.HasValue())
            {
                if (!KubernetesUtilities.IsValidK8sObjectName(targetDeploymentOption.Value()))
                {
                    throw new InvalidUsageException(_operationContext, Resources.Error_InvalidConfigurationValue, targetDeploymentOption.Template);
                }
                _operationContext.LoggingProperties.Add(CliConstants.Properties.TargetDeploymentName, new PII(targetDeploymentOption.Value()));
                _targetDeployment = targetDeploymentOption.Value();
            }
            if (targetServiceOption.HasValue())
            {
                if (!KubernetesUtilities.IsValidK8sObjectName(targetServiceOption.Value()))
                {
                    throw new InvalidUsageException(_operationContext, Resources.Error_InvalidConfigurationValue, targetServiceOption.Template);
                }
                _operationContext.LoggingProperties.Add(CliConstants.Properties.TargetServiceName, new PII(targetServiceOption.Value()));
                _targetService = targetServiceOption.Value();
            }
            if (targetNamespaceOption.HasValue())
            {
                if (!KubernetesUtilities.IsValidK8sObjectName(targetNamespaceOption.Value()))
                {
                    throw new InvalidUsageException(_operationContext, Resources.Error_InvalidConfigurationValue, targetNamespaceOption.Template);
                }
                _targetNamespace = targetNamespaceOption.Value();
            }
            if (targetKubeConfigContextOption.HasValue())
            {
                _targetKubeConfigContext = targetKubeConfigContextOption.Value();
            }
        }

        protected RemoteContainerConnectionDetails ResolveContainerConnectionDetails(string routingHeaderValue, IEnumerable<string> routingManagerFeatureFlags)
        {
            RemoteContainerConnectionDetails remoteContainerConnectionDetails = null;

            // Local process debugging
            if (!string.IsNullOrEmpty(_targetPod))
            {
                remoteContainerConnectionDetails = RemoteContainerConnectionDetails.ReplacingExistingContainerInPod(
                    namespaceName: _targetNamespace,
                    podName: _targetPod,
                    containerName: _targetContainer);
            }
            else if (!string.IsNullOrEmpty(_targetDeployment))
            {
                remoteContainerConnectionDetails = RemoteContainerConnectionDetails.ReplacingExistingContainerInDeployment(
                    namespaceName: _targetNamespace,
                    deploymentName: _targetDeployment,
                    containerName: _targetContainer);
            }
            else if (!string.IsNullOrEmpty(_targetService) && string.IsNullOrEmpty(routingHeaderValue))
            {
                remoteContainerConnectionDetails = RemoteContainerConnectionDetails.ReplacingExistingContainerInService(
                    namespaceName: _targetNamespace,
                    serviceName: _targetService,
                    containerName: _targetContainer);
            }
            else if (!string.IsNullOrEmpty(_targetService) && !string.IsNullOrEmpty(routingHeaderValue))
            {
                remoteContainerConnectionDetails = RemoteContainerConnectionDetails.CreatingNewPodWithContextFromExistingService(
                    namespaceName: _targetNamespace, // For routing, the source namespace and target namespace are the same.
                    serviceName: _targetService,
                    containerName: _targetContainer,
                    routingHeaderValue: routingHeaderValue,
                    routingManagerFeatureFlags: routingManagerFeatureFlags);
            }
            return remoteContainerConnectionDetails;
        }
    }
}