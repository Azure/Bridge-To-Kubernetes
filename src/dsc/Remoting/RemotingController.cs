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
    [Route("api/[controller]")]
    [ApiController]
    public class RemotingController : ControllerBase
    {
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILog _log;

        public RemotingController(IHostApplicationLifetime lifetime, ILog log) { 
            _lifetime = lifetime;
            _log = log;
            _log.Info("Remoting Controller Constructor called");
        }

        [HttpPost("stop")]
        public IActionResult Stop()
        {
            try
            {
                _log.Info("stop controller method is called");
                RemotingHelper.Stop();
                //_lifetime.StopApplication();
                return this.Ok();
            }
            catch (Exception)
            {
                return this.StatusCode(StatusCodes.Status500InternalServerError, Resources.RemotingAPI_StopFailed);
            }
        }
    }
}