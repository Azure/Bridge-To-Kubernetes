// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.Json;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Common.Models.Kubernetes
{
    /// <summary>
    /// Kubernetes Pod
    /// </summary>
    public class Pod
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="T:Microsoft.BridgeToKubernetes.Common.Models.Kubernetes.Pod"/> class.
        /// </summary>
        public Pod(string name, IDictionary<string, string> labels, string status, string configName, DateTime lastUpdateTimestamp, Container userContainer, bool isBuildContainerRunning)
        {
            Name = name;
            Labels = labels;
            Status = status;
            ConfigName = configName;
            LastUpdateTimestamp = lastUpdateTimestamp;
            UserContainer = userContainer;
            IsBuildContainerRunning = isBuildContainerRunning;
        }

        /// <summary>
        /// Gets or sets the Pod name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets or sets Pod labels
        /// </summary>
        public IDictionary<string, string> Labels { get; }

        /// <summary>
        /// Gets or sets Pod status
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets or sets Pod config name
        /// </summary>
        public string ConfigName { get; }

        /// <summary>
        /// Gets or sets Pod's last updated timestamp
        /// </summary>
        public DateTime LastUpdateTimestamp { get; }

        /// <summary>
        /// Gets or sets Pod's user container
        /// </summary>
        public Container UserContainer { get; }

        /// <summary>
        /// Gets or sets flag whether build container is running
        /// </summary>
        public bool IsBuildContainerRunning { get; }

        /// <summary>
        /// Sorts pods by name, then by user container ID
        /// </summary>
        /// <param name="pods"></param>
        /// <returns>Sorted list of pods</returns>
        public static IList<Pod> GetOrderedList(IEnumerable<Pod> pods)
        {
            // Sort by Pod name and then by container Id
            return pods == null ?
                    new List<Pod>() :
                    pods.OrderBy(pod => pod.Name)
                         .ThenBy(pod => pod.UserContainer.Id).ToList();
        }

        internal IDictionary<string, object> GetEventParameters()
        {
            return new Dictionary<string, object>
                  {
                      { nameof(Name), new PII(Name) },
                      { nameof(Labels), new PII(JsonHelpers.SerializeObject(Labels)) },
                      { nameof(Status),  Status },
                      { nameof(ConfigName),  ConfigName },
                      { nameof(LastUpdateTimestamp), LastUpdateTimestamp },
                      { nameof(UserContainer), UserContainer.GetEventParameters() }
                  };
        }
    }
}