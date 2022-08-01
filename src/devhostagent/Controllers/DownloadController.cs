// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.DevHostAgent.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    internal class DownloadController : ControllerBase
    {
        private readonly ILog _log;

        public DownloadController(ILog log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        [HttpGet("files/{*path}")]
        public IActionResult DownloadFiles([FromRoute] string path)
        {
            if (!path.StartsWith('/'))
            {
                path = '/' + path;
            }

            string zipFileName = Guid.NewGuid().ToString().Substring(6) + ".tar.gz";
            string zipFilePath = Path.Combine(Path.GetTempPath(), zipFileName);

            int retCode;
            if (System.IO.File.Exists(path))
            {
                retCode = this.RunTarFile(zipFilePath, path);
                this.Response.Headers.Add(Constants.HeaderName_DownloadIsFile, "file");
            }
            else
            {
                if (!Directory.Exists(path))
                {
                    return NotFound();
                }
                retCode = this.RunTarDirectory(zipFilePath, path);
            }
            if (retCode != 0)
            {
                throw new InvalidOperationException();
            }

            var fileStream = System.IO.File.OpenRead(zipFilePath);
            return new FileStreamResult(fileStream, "application/gzip");
        }

        private int RunTarDirectory(string targetFile, string sourceDirectory)
        {
            return this.RunTar($"czfh {targetFile} -C {sourceDirectory} .");
        }

        private int RunTarFile(string targetFile, string sourceFile)
        {
            return this.RunTar($"czfh {targetFile} {sourceFile}");
        }

        private int RunTar(string commandLine)
        {
            ProcessStartInfo psi = new ProcessStartInfo()
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                FileName = "tar",
                Arguments = commandLine
            };

            StringBuilder sb = new StringBuilder();
            Process proc = new Process();
            proc.StartInfo = psi;
            proc.OutputDataReceived += (sender, e) => { sb.Append(e.Data); };
            proc.ErrorDataReceived += (sender, e) => { sb.Append(e.Data); };

            proc.Start();
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            proc.WaitForExit();

            if (proc.ExitCode == 0)
            {
                _log.Info($"SyncService.RunTar tar succeeded. {sb}");
            }
            else
            {
                _log.Error($"SyncService.RunTar tar failed with {proc.ExitCode}, {sb}");
            }
            return proc.ExitCode;
        }
    }
}