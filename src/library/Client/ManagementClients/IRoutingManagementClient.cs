// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.BridgeToKubernetes.Common.Models;
using Microsoft.BridgeToKubernetes.Library.Models;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    public interface IRoutingManagementClient : IDisposable
    {
        /// <summary>
        /// Deploy routing manager to specified namespace.
        /// </summary>
        /// <param name="cancellationToken"></param>
        Task<OperationResponse> DeployRoutingManagerAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Get status of routing manager deployment in the specified namespace.
        /// </summary>
        /// <param name="podName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>RoutingStatus object which has attribute IsConnected that returns true if the routing manager is deployed successfully in the specified namespace.</returns>
        Task<OperationResponse<RoutingStatus>> GetStatusAsync(string podName, CancellationToken cancellationToken);

        /// <summary>
        /// Get validation errors for routing scenario, if any
        /// </summary>
        /// <returns></returns>
        Task<OperationResponse<string>> GetValidationErrorsAsync(string routeAsHeaderValue, CancellationToken cancellationToken);
    }
}