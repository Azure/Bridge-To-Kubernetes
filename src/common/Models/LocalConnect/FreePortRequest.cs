// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// An elevated access request for freeing ports
    /// </summary>
    public class FreePortRequest : ElevationRequest, IFreePortRequest
    {
        private IList<IPortMapping> _occupiedPorts = new List<IPortMapping>();

        /// <summary>
        /// List of occupied ports
        /// </summary>
        public IList<IPortMapping> OccupiedPorts => _occupiedPorts;

        /// <summary>
        /// <see cref="IElevationRequest.RequestType"/>
        /// </summary>
        public override ElevationRequestType RequestType => ElevationRequestType.FreePort;

        // Defining this internal constructor prevents external code from instantiating this class
        internal FreePortRequest()
        {
        }

        /// <summary>
        /// <see cref="IElevationRequest.ConvertToReadableString"/>
        /// </summary>
        public override string ConvertToReadableString()
        {
            var sb = new StringBuilder();
            if (this._occupiedPorts.Any())
            {
                if (_occupiedPorts.FirstOrDefault() is ServicePortMapping)
                {
                    foreach (ServicePortMapping op in _occupiedPorts)
                    {
                        sb.AppendLine($" - {string.Format(CommonResources.ElevationRequest_FreePortRequestFormat, op.ServiceName, op.PortNumber)}");
                    }
                }
                else if (_occupiedPorts.FirstOrDefault() is ProcessPortMapping)
                {
                    foreach (ProcessPortMapping op in _occupiedPorts)
                    {
                        sb.AppendLine($"- {string.Format(CommonResources.ElevationRequest_FreePortRequestFormat, op.ProcessName, op.PortNumber)}");
                    }
                }
                else
                {
                    throw new NotSupportedException($"Unsupported mapping found in occupied ports: '{_occupiedPorts.FirstOrDefault().GetType()}'");
                }
            }
            return sb.ToString();
        }
    }
}