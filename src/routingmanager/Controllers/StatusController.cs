// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using Microsoft.BridgeToKubernetes.Common.Models;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Controllers
{
    /// <summary>
    /// Controller for returning the status of Routing Manager
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        public StatusController()
        {
        }

        /// <summary>
        /// Returns the status of routing manager
        /// </summary>
        [HttpGet]
        public RoutingStatus Get([FromQuery] string devhostAgentPodName)
        {
            if (!string.IsNullOrEmpty(RoutingManagerApp.Status.RoutingErrorMessage))
            {
                return new RoutingStatus(false, RoutingManagerApp.Status.RoutingErrorMessage);
            }

            if (RoutingManagerApp.Status.EntityTriggerNamesStatus.TryGetValue(devhostAgentPodName, out string statusValue))
            {
                return new RoutingStatus(string.IsNullOrWhiteSpace(statusValue), statusValue);
            }

            // IsConnected is null when there is no entry for the devhostagent pod in status dictionary in routing manager yet
            return new RoutingStatus(null, Common.Constants.Routing.InvalidValueOfTriggerError + devhostAgentPodName);
        }
    }
}