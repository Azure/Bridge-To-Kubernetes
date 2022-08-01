// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.BridgeToKubernetes.Exe;
using Microsoft.BridgeToKubernetes.Exe.Remoting;

namespace Microsoft.BridgeToKubernetes.Remoting
{
    [Route("api/[controller]")]
    [ApiController]
    public class RemotingController : ControllerBase
    {
        [HttpPost("stop")]
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