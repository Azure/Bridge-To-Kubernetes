// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;

namespace Microsoft.BridgeToKubernetes.RoutingManager.Controllers
{
    /// <summary>
    /// Controller for ping
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PingController : ControllerBase
    {
        public PingController()
        {
        }

        [HttpGet]
        public string Get()
        {
            return "Ping!!";
        }
    }
}