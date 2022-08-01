// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;

namespace Microsoft.BridgeToKubernetes.Library.Models
{
    /// <summary>
    /// Object model that contains the patch state information for a connect session
    /// </summary>
    internal class PatchState
    {
        private readonly ConcurrentDictionary<string, DeploymentPatch> _deploymentPatches = new ConcurrentDictionary<string, DeploymentPatch>();
        private readonly ConcurrentDictionary<string, StatefulSetPatch> _statefulSetPatches = new ConcurrentDictionary<string, StatefulSetPatch>();
        private readonly ConcurrentDictionary<string, PodPatch> _podPatches = new ConcurrentDictionary<string, PodPatch>();
        private readonly ConcurrentDictionary<string, PodDeployment> _podDeployments = new ConcurrentDictionary<string, PodDeployment>();

        /// <summary>
        /// All deployment patch operations
        /// </summary>
        public IEnumerable<DeploymentPatch> DeploymentPatches => _deploymentPatches.Values;

        /// <summary>
        /// All pod patch operations
        /// </summary>
        public IEnumerable<PodPatch> PodPatches => _podPatches.Values;

        /// <summary>
        /// All deployed pods
        /// </summary>
        public IEnumerable<PodDeployment> PodDeployments => _podDeployments.Values;

        /// <summary>
        /// All statefulset patch operations
        /// </summary>
        public IEnumerable<StatefulSetPatch> StatefulSetPatches => _statefulSetPatches.Values;

        /// <summary>
        /// Clears all entities
        /// </summary>
        public void Clear()
        {
            _deploymentPatches.Clear();
            _podPatches.Clear();
            _podDeployments.Clear();
        }

        #region DeploymentPatch

        /// <summary>
        /// Adds a deployment patch operation
        /// </summary>
        public void AddDeploymentPatch(DeploymentPatch deploymentPatch)
            => _deploymentPatches[_GetKey(deploymentPatch.Deployment)] = deploymentPatch;

        /// <summary>
        /// Attempts to remove a deployment patch operation for a given deployment
        /// </summary>
        public bool TryRemoveDeploymentPatch(V1Deployment deployment)
            => _deploymentPatches.TryRemove(_GetKey(deployment), out _);

        #endregion DeploymentPatch

        #region StatefulSetPatch

        /// <summary>
        /// Adds a stateful set patch operation
        /// </summary>
        public void AddStatefulSetPatch(StatefulSetPatch statefulSetPatch)
            => _statefulSetPatches[_GetKey(statefulSetPatch.StatefulSet)] = statefulSetPatch;

        /// <summary>
        /// Attempts to remove a stateful set patch operation for a given stateful set
        /// </summary>
        public bool TryRemoveStatefulSetPatch(V1StatefulSet statefulSet)
            => _statefulSetPatches.TryRemove(_GetKey(statefulSet), out _);

        #endregion StatefulSetPatch

        #region PodPatch

        /// <summary>
        /// Adds a pod patch operation
        /// </summary>
        public void AddPodPatch(PodPatch podPatch)
            => _podPatches[_GetKey(podPatch.Pod)] = podPatch;

        /// <summary>
        /// Attempts to remove a pod patch operation for a given pod
        /// </summary>
        public bool TryRemovePodPatch(V1Pod pod)
            => _podPatches.TryRemove(_GetKey(pod), out _);

        #endregion PodPatch

        #region Deployed pod

        /// <summary>
        /// Adds a deployed pod record
        /// </summary>
        public void AddPodDeployment(PodDeployment podDeployment)
            => _podDeployments[_GetKey(podDeployment.Pod)] = podDeployment;

        /// <summary>
        /// Attempts to remove a deployed pod record
        /// </summary>
        public void TryRemovePodDeployment(V1Pod pod)
            => _podDeployments.TryRemove(_GetKey(pod), out _);

        #endregion Deployed pod

        /// <summary>
        /// Gets a key used to uniquely identify the given kubernetes object
        /// </summary>
        private static string _GetKey(IKubernetesObject<V1ObjectMeta> kubernetesObject)
            => $"{kubernetesObject.ApiVersion}/{kubernetesObject.Kind}/{kubernetesObject.Namespace()}/{kubernetesObject.Name()}";
    }
}