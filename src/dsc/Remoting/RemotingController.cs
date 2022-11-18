// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.BridgeToKubernetes.Exe;
using Microsoft.BridgeToKubernetes.Exe.Remoting;
using Microsoft.Extensions.Hosting;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Remoting
{
    [ApiController]
    [Route("[controller]")]
    public class RemotingController : ControllerBase
    {

        [HttpGet("stop")]
        public IActionResult Stop()
        {
            try
            {
                RemotingHelper.Stop();
                return this.Ok();
            }
            catch (Exception)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, Resources.RemotingAPI_StopFailed);
            }
        }
    }
}