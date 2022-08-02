// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.BridgeToKubernetes.Common.Models.LocalConnect
{
    /// <summary>
    /// A request for elevated permissions in order to edit the hosts file on the machine
    /// </summary>
    public class EditHostsFileRequest : ElevationRequest, IEditHostsFileRequest
    {
        /// <summary>
        /// <see cref="IElevationRequest.RequestType"/>
        /// </summary>
        public override ElevationRequestType RequestType => ElevationRequestType.EditHostsFile;

        // Defining this internal constructor prevents external code from instantiating this class
        internal EditHostsFileRequest()
        {
        }

        /// <summary>
        /// <see cref="IElevationRequest.ConvertToReadableString"/>
        /// </summary>
        public override string ConvertToReadableString()
        {
            return $"- {CommonResources.ElevationRequest_EditHostsFile}";
        }
    }
}