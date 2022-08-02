// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using Microsoft.BridgeToKubernetes.Common.Models.DevHost;
using Microsoft.BridgeToKubernetes.DevHostAgent.Services;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.Controllers
{
    /// <summary>
    /// Controller for getting information on the connected clients
    /// </summary>
    /// <remarks>this is used by the restoreJob to figure out when it is time to remove the RemoteAgent</remarks>
    [Route("api/[controller]")]
    [ApiController]
    public class ConnectedSessionsController : ControllerBase
    {
        [HttpGet]
        public ConnectedSessionsResponseModel GetConnectedClients()
        {
            var response = new ConnectedSessionsResponseModel()
            {
                NumConnectedSessions = AgentExecutorHub.NumConnectedSessions
            };
            return response;
        }
    }
}