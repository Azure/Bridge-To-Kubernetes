// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Microsoft.BridgeToKubernetes.LocalAgent.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NetworkDetailsController : ControllerBase
    {
        private readonly ILogger<NetworkDetailsController> _logger;

        public NetworkDetailsController(ILogger<NetworkDetailsController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public NetworkDetails Get()
        {
            return new NetworkDetails
            {
                RemoteIP = HttpContext.Connection.RemoteIpAddress.MapToIPv4().ToString(),
                LocalIP = HttpContext.Connection.LocalIpAddress.MapToIPv4().ToString(),
                RemotePort = HttpContext.Connection.RemotePort,
                LocalPort = HttpContext.Connection.LocalPort,
                Host = HttpContext.Request.Host.Host
            };
        }
    }
}