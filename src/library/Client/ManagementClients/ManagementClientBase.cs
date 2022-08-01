// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using Microsoft.BridgeToKubernetes.Common;
using Microsoft.BridgeToKubernetes.Common.Logging;

namespace Microsoft.BridgeToKubernetes.Library.ManagementClients
{
    /// <summary>
    /// Base class to handle log flushing for management clients
    /// </summary>
    internal abstract class ManagementClientBase : ServiceBase
    {
        protected readonly ILog _log;
        protected IOperationContext _operationContext;
        private bool _disposed = false;

        public ManagementClientBase(ILog log, IOperationContext operationContext, bool autoDisposeEnabled = true)
            : base(autoDisposeEnabled)
        {
            this._log = log ?? throw new ArgumentNullException(nameof(log));
            this._operationContext = operationContext ?? throw new ArgumentNullException(nameof(operationContext));
        }

        ~ManagementClientBase()
        {
            this.Dispose();
        }

        public override void Dispose()
        {
            if (this._disposed)
            {
                return;
            }
            this._disposed = true;

            this._log.Flush(TimeSpan.FromMilliseconds(1500));
            base.Dispose();
        }
    }
}