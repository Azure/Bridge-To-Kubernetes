// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.IO;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Kubernetes;
using Microsoft.BridgeToKubernetes.Common.Logging;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using Microsoft.BridgeToKubernetes.Common.Restore;
using Microsoft.BridgeToKubernetes.Common.Utilities;
using Microsoft.BridgeToKubernetes.Library.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using static Microsoft.BridgeToKubernetes.Common.Constants;
using static Microsoft.BridgeToKubernetes.Common.DevHostAgent.DevHostConstants;

namespace Microsoft.BridgeToKubernetes.Library.Connect
{
    /// <summary>
    /// <see cref="IRemoteRestoreJobDeployer"/>
    /// </summary>
    internal class RemoteRestoreJobDeployer : RemoteRestoreJobCleaner, IRemoteRestoreJobDeployer
    {
        /// <summary>
        /// NOTE: Make sure to bump this version if you make any changes to the
        /// ServiceAccount, Role, or RoleBinding.
        /// </summary>
        private const string RbacResourceVersion = "v2";

        private readonly Lazy<IImageProvider> _imageProvider;
        private readonly IFileSystem _fileSystem;
        private readonly IEnvironmentVariables _environmentVariables;
        private readonly IOperationContext _operationContext;

        private bool _rbacEnabled = true;

        public RemoteRestoreJobDeployer(
            IKubernetesClient kubernetesClient,
            Lazy<IImageProvider> imageProvider,
            IFileSystem fileSystem,
            ILog log,
            IOperationContext operationContext,
            IEnvironmentVariables environmentVariables)
            : base(kubernetesClient, log)
        {
            _imageProvider = imageProvider;
            _fileSystem = fileSystem;
            _environmentVariables = environmentVariables;
            _operationContext = operationContext;
        }

        /// <summary>
        /// <see cref="IRemoteRestoreJobDeployer.CreateRemoteRestoreJobAsync"/>
        /// </summary>
        public async Task CreateRemoteRestoreJobAsync<T>(string targetName, string namespaceName, T patch, CancellationToken cancellationToken) where T : PatchEntityBase
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                Events.RemoteRestoreJobDeployer.AreaName,
                Events.RemoteRestoreJobDeployer.Operations.Deploy))
            {
                try
                {
                    await _EnsureRbacResourcesAsync(namespaceName, cancellationToken);

                    string jobName = _GetJobName(targetName);
                    string instanceLabel = GetInstanceLabel(targetName);

                    // Try to clean up existing job if exists
                    await CleanupInnerAsync(instanceLabel, namespaceName, cancellationToken);

                    // Deploy
                    await _kubernetesClient.CreateNamespacedSecretAsync(namespaceName, this._GetSecretSpec(jobName, namespaceName, instanceLabel, patch), cancellationToken);
                    _log.Info("Created restore job secret");
                    await _kubernetesClient.CreateNamespacedJobAsync(namespaceName, this._GetJobSpec(jobName, namespaceName, instanceLabel), cancellationToken);
                    _log.Info("Created restore job workload");

                    perfLogger.SetSucceeded();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    perfLogger.SetCancelled();
                    throw;
                }
            }
        }

        /// <summary>
        /// <see cref="IRemoteRestoreJobDeployer.TryGetExistingPatchStateAsync"/>
        /// </summary>
        public async Task<T> TryGetExistingPatchInfoAsync<T>(string targetName, string namespaceName, CancellationToken cancellationToken) where T : PatchEntityBase
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                Events.RemoteRestoreJobDeployer.AreaName,
                Events.RemoteRestoreJobDeployer.Operations.TryGetExistingRestoreJobInfo))
            {
                try
                {
                    V1Secret secret;
                    try
                    {
                        string jobName = _GetJobName(targetName);
                        secret = await _kubernetesClient.ReadNamespacedSecretAsync(namespaceName, jobName, cancellationToken);
                        if (secret == null)
                        {
                            _log.Info("Existing restore job secret not found");
                            perfLogger.SetSucceeded();
                            return null;
                        }
                        _log.Info("Found existing restore job secret");
                    }
                    catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.NotFound)
                    {
                        _log.Info("Existing restore job secret not found");
                        perfLogger.SetSucceeded();
                        return null;
                    }

                    try
                    {
                        string json = secret.Data[_fileSystem.Path.GetFileName(DevHostRestorationJob.PatchStateFullPath)].Utf8ToString();
                        var patch = JsonHelpers.DeserializeObject<T>(json);
                        perfLogger.SetSucceeded();
                        return patch;
                    }
                    catch (Exception e)
                    {
                        _log.Error("Malformed restore job secret");
                        _log.Exception(e);
                        return null;
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    perfLogger.SetCancelled();
                    throw;
                }
            }
        }

        /// <summary>
        /// Ensures the necessary RBAC resources are deployed to the namespace
        /// </summary>
        private async Task _EnsureRbacResourcesAsync(string namespaceName, CancellationToken cancellationToken)
        {
            using (var perfLogger = _log.StartPerformanceLogger(
                Events.RemoteRestoreJobDeployer.AreaName,
                Events.RemoteRestoreJobDeployer.Operations.EnsureRbacResources))
            {
                try
                {
                    await _kubernetesClient.CreateServiceAccountIfNotExists(namespaceName, this._GetServiceAccountSpec(namespaceName), cancellationToken);
                    _log.Info("Service account created/refreshed");

                    const string RbacEnabled = "RbacEnabled";
                    perfLogger.SetProperty(RbacEnabled, _rbacEnabled);
                    if (_rbacEnabled)
                    {
                        try
                        {
                            await _kubernetesClient.CreateOrReplaceV1RoleInNamespaceAsync(this._GetRoleSpec(namespaceName), namespaceName, cancellationToken);
                            _log.Info("Role created/refreshed");
                            await _kubernetesClient.CreateOrReplaceV1RoleBindingInNamespaceAsync(this._GetRoleBindingSpec(namespaceName), namespaceName, cancellationToken);
                            _log.Info("Role binding created/refreshed");
                        }
                        catch (HttpOperationException e) when (e.Response.StatusCode == HttpStatusCode.UnprocessableEntity)
                        {
                            _rbacEnabled = false;
                            _log.Info("This is a non-rbac cluster, ignoring errors from creating role and role binding. Returning.");
                            // If we got 422 for the role, skip trying to create RoleBinding
                            perfLogger.SetProperty(RbacEnabled, false);
                            perfLogger.SetSucceeded();
                            return;
                        }
                    }

                    perfLogger.SetSucceeded();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    perfLogger.SetCancelled();
                    throw;
                }
            }
        }

        /// <summary>
        /// Gets the name of this instance's resources
        /// </summary>
        private static string _GetJobName(string targetName)
            => KubernetesUtilities.GetKubernetesResourceName(targetName, suffix: $"-restore-{targetName.Sha256Hash(5)}");

        #region Template retrieval

        /// <summary>
        /// Creates a job spec for the restoration job
        /// </summary>
        private V1Job _GetJobSpec(string jobName, string namespaceName, string instanceLabel)
        {
            string jobTemplate =
                $@"
                apiVersion: batch/v1
                kind: Job
                metadata:
                  name: {jobName}
                  namespace: {namespaceName}
                  labels:
                    {Labels.VersionLabelName}: ""{ImageProvider.DevHostRestorationJob.Version}""
                    {Labels.ComponentLabelName}: {DevHostRestorationJob.ObjectNameLower}
                    {Labels.InstanceLabelName}: {instanceLabel}
                spec:
                  backoffLimit: 10
                  template:
                    metadata:
                      labels:
                        {Labels.VersionLabelName}: ""{ImageProvider.DevHostRestorationJob.Version}""
                        {Labels.ComponentLabelName}: {DevHostRestorationJob.ObjectNameLower}
                        {Labels.InstanceLabelName}: {instanceLabel}
                    spec:
                      volumes:
                      - name: patchstate
                        secret:
                          secretName: {jobName}
                      nodeSelector:
                        {KubernetesConstants.Labels.OS}: {KubernetesConstants.Labels.Values.Linux}
                      containers:
                      - name: {DevHostRestorationJob.ObjectNameLower}
                        image: {_imageProvider.Value.DevHostRestorationJobImage}
                        imagePullPolicy: Always
                        env:
                        - name: NAMESPACE
                          valueFrom:
                            fieldRef:
                              fieldPath: metadata.namespace
                        - name: INSTANCE_LABEL_VALUE
                          value: ""{instanceLabel}""
                        - name: ""{EnvironmentVariables.Names.BridgeEnvironment}""
                          value: ""{_environmentVariables.ReleaseEnvironment}""
                        - name: ""{EnvironmentVariables.Names.CollectTelemetry}""
                          value: ""{_environmentVariables.CollectTelemetry}""
                        - name: ""{EnvironmentVariables.Names.CorrelationId}""
                          value: ""{_operationContext.CorrelationId}""
                        volumeMounts:
                        - name: patchstate
                          mountPath: {_fileSystem.Path.GetDirectoryName(DevHostRestorationJob.PatchStateFullPath).Replace('\\', '/')}
                          readOnly: true
                      restartPolicy: OnFailure
                      serviceAccount: {DevHostRestorationJob.ObjectNameLower}-{RbacResourceVersion}
                      serviceAccountName: {DevHostRestorationJob.ObjectNameLower}-{RbacResourceVersion}
                ";

            var job = KubernetesYaml.Deserialize<V1Job>(jobTemplate);
            return job;
        }

        /// <summary>
        /// Creates a secret spec containing the patch state
        /// </summary>
        private V1Secret _GetSecretSpec(string jobName, string namespaceName, string instanceLabel, PatchEntityBase patch)
        {
            string secretTemplate =
                $@"
                apiVersion: v1
                kind: Secret
                metadata:
                  name: {jobName}
                  namespace: {namespaceName}
                  labels:
                    {Labels.VersionLabelName}: ""{ImageProvider.DevHostRestorationJob.Version}""
                    {Labels.ComponentLabelName}: {DevHostRestorationJob.ObjectNameLower}
                    {Labels.InstanceLabelName}: {instanceLabel}
                data: {{ }}
                ";

            var secret = KubernetesYaml.Deserialize<V1Secret>(secretTemplate);
            secret.StringData = new Dictionary<string, string>()
            {
                { _fileSystem.Path.GetFileName(DevHostRestorationJob.PatchStateFullPath), JsonHelpers.SerializeObjectIndented(patch) }
            };
            return secret;
        }

        /// <summary>
        /// Creates a service account spec for the given namespace
        /// </summary>
        private V1ServiceAccount _GetServiceAccountSpec(string namespaceName)
        {
            string serviceAccountTemplate =
                $@"
                apiVersion: v1
                kind: ServiceAccount
                metadata:
                  name: {DevHostRestorationJob.ObjectNameLower}-{RbacResourceVersion}
                  namespace: {namespaceName}
                  labels:
                    {Labels.VersionLabelName}: ""{RbacResourceVersion}""
                    {Labels.ComponentLabelName}: {DevHostRestorationJob.ObjectNameLower}
                ";

            var serviceAccount = KubernetesYaml.Deserialize<V1ServiceAccount>(serviceAccountTemplate);
            return serviceAccount;
        }

        /// <summary>
        /// Creates a role spec for the given namespace
        /// </summary>
        private V1Role _GetRoleSpec(string namespaceName)
        {
            string roleTemplate =
                $@"
                apiVersion: rbac.authorization.k8s.io/v1
                kind: Role
                metadata:
                  name: {DevHostRestorationJob.ObjectNameLower}-role-{RbacResourceVersion}
                  namespace: {namespaceName}
                  labels:
                    {Labels.VersionLabelName}: ""{RbacResourceVersion}""
                    {Labels.ComponentLabelName}: {DevHostRestorationJob.ObjectNameLower}
                rules:
                - apiGroups: [""""]
                  resources: [""pods""]
                  verbs: [""get"", ""list"", ""update"", ""patch"", ""delete""]

                - apiGroups: [""extensions"", ""apps""]
                  resources: [""deployments"", ""statefulsets""]
                  verbs: [""get"", ""list"", ""update"", ""patch""]

                - apiGroups: [""extensions"", ""apps""]
                  resources: [""replicasets""]
                  verbs: [""get"", ""list""]

                - apiGroups: [""""]
                  resources: [""secrets""]
                  verbs: [""delete"", ""list""]

                - apiGroups: [""batch""]
                  resources: [""jobs""]
                  verbs: [""delete"", ""list""]
                ";

            var role = KubernetesYaml.Deserialize<V1Role>(roleTemplate);
            return role;
        }

        /// <summary>
        /// Creates a role binding spec for the given namespace
        /// </summary>
        private V1RoleBinding _GetRoleBindingSpec(string namespaceName)
        {
            string roleBindingTemplate =
                $@"
                apiVersion: rbac.authorization.k8s.io/v1
                kind: RoleBinding
                metadata:
                  name: {DevHostRestorationJob.ObjectNameLower}-binding-{RbacResourceVersion}
                  namespace: {namespaceName}
                  labels:
                    {Labels.VersionLabelName}: ""{RbacResourceVersion}""
                    {Labels.ComponentLabelName}: {DevHostRestorationJob.ObjectNameLower}
                subjects:
                - kind: ServiceAccount
                  name: {DevHostRestorationJob.ObjectNameLower}-{RbacResourceVersion}
                  namespace: {namespaceName}
                roleRef:
                  kind: Role
                  name: {DevHostRestorationJob.ObjectNameLower}-role-{RbacResourceVersion}
                  apiGroup: rbac.authorization.k8s.io
                ";

            var roleBinding = KubernetesYaml.Deserialize<V1RoleBinding>(roleBindingTemplate);
            return roleBinding;
        }

        #endregion Template retrieval
    }
}