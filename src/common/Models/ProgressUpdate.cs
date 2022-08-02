// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models
{
    /// <summary>
    /// Notifies integrators of updates in the progress of operations
    /// </summary>
    public class ProgressUpdate
    {
        // TODO: Make the completion percentage deterministic
        // ProgressUpdate with PercentageCompletion = -1 denotes failure
        // It can help point out the last point of failure
        /// <summary>
        /// Constructor
        /// </summary>
        public ProgressUpdate(int percentageCompletion,
                            ProgressStatus progressStatus,
                            ProgressMessage progressMessage = null)
        {
            this.PercentageCompletion = percentageCompletion;
            this.ProgressStatus = progressStatus;
            this.ProgressMessage = progressMessage;
        }

        /// <summary>
        /// Progress status
        /// </summary>
        public ProgressStatus ProgressStatus { get; }

        /// <summary>
        /// Message for the user about the progress
        /// </summary>
        public ProgressMessage ProgressMessage { get; }

        /// <summary>
        /// How much of the overall operation is complete 0-100
        /// </summary>
        public int PercentageCompletion { get; }

        /// <summary>
        /// Indicates whether the <see cref="ProgressMessage"/> should be displayed to the user
        /// </summary>
        public bool ShouldPrintMessage => !string.IsNullOrEmpty(ProgressMessage?.Message) || (ProgressMessage?.NewLine ?? false);
    }

    /// <summary>
    /// Clients can choose to handle the progress update and display the completion percentage
    /// It can be used for example:
    /// if (ProgressStatus > ProgressStatus.None)
    /// {
    ///     . . .
    /// }
    /// </summary>
    public enum ProgressStatus
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// KubernetesRemoteEnvironmentManager
        /// </summary>
        KubernetesRemoteEnvironmentManager,

        /// <summary>
        /// LocalConnect
        /// </summary>
        LocalConnect,

        /// <summary>
        /// EndpointManagementClient
        /// </summary>
        EndpointManagementClient,

        /// <summary>
        /// WorkloadInformationProvider
        /// </summary>
        WorkloadInformationProvider
    }
}