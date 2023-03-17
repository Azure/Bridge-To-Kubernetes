// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.BridgeToKubernetes.Common.IO.Output;
using Microsoft.BridgeToKubernetes.Common.Models.LocalConnect;
using System.Text.Json.Serialization;

namespace Microsoft.BridgeToKubernetes.Exe.Output.Models
{
    internal class ElevationRequestData
    {
        private static class TargetTypes
        {
            public const string Service = "service";
            public const string Process = "process";
        }

        public string RequestType { get; }

        [TableOutputFormatIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<PortInformationData> TargetPortInformation { get; }

        [TableOutputFormatIgnore]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string TargetType { get; }

        [JsonConstructor]
        public ElevationRequestData(string requestType, IEnumerable<PortInformationData> targetPortInformation = null, string targetType = null)
        {
            this.RequestType = requestType;
            this.TargetPortInformation = targetPortInformation;
            this.TargetType = targetType;
        }

        public ElevationRequestData(IElevationRequest elevationRequest)
        {
            this.RequestType = elevationRequest.RequestType.GetStringValue();
            if (elevationRequest.RequestType == ElevationRequestType.FreePort)
            {
                var freePortRequest = elevationRequest as FreePortRequest;
                var occupiedPorts = freePortRequest.OccupiedPorts;
                if (occupiedPorts.Any())
                {
                    if (occupiedPorts.FirstOrDefault() is ServicePortMapping)
                    {
                        this.TargetType = TargetTypes.Service;
                        this.TargetPortInformation = occupiedPorts.Select(op => new PortInformationData(op as ServicePortMapping));
                    }
                    else
                    {
                        this.TargetType = TargetTypes.Process;
                        this.TargetPortInformation = occupiedPorts.Select(op => new PortInformationData(op as ProcessPortMapping));
                    }
                }
            }
        }

        public IElevationRequest ConvertToElevationRequest()
        {
            IElevationRequest elevationRequest;
            ElevationRequestType elevationRequestType = (ElevationRequestType)Enum.Parse(typeof(ElevationRequestType), this.RequestType, ignoreCase: true);
            switch (elevationRequestType)
            {
                case ElevationRequestType.EditHostsFile:
                    elevationRequest = new EditHostsFileRequest();
                    break;

                case ElevationRequestType.FreePort:
                    var freePortRequest = new FreePortRequest();
                    var targetType = this.TargetType;
                    if (StringComparer.OrdinalIgnoreCase.Equals(targetType, TargetTypes.Service))
                    {
                        this.TargetPortInformation
                            .Select(portInformation => new ServicePortMapping(portInformation.Name, portInformation.Port, portInformation.ProcessId))
                            .ExecuteForEach(portMapping => freePortRequest.OccupiedPorts.Add(portMapping));
                    }
                    else if (StringComparer.OrdinalIgnoreCase.Equals(targetType, TargetTypes.Process))
                    {
                        this.TargetPortInformation
                            .Select(portInformation => new ProcessPortMapping(portInformation.Name, portInformation.Port, portInformation.ProcessId))
                            .ExecuteForEach(portMapping => freePortRequest.OccupiedPorts.Add(portMapping));
                    }
                    else
                    {
                        throw new Exception($"Invalid elevation request data. Unknown target type: '{targetType}'");
                    }

                    elevationRequest = freePortRequest;
                    break;

                default:
                    throw new Exception("Invalid elevation request data. Unsupported elevation request type.");
            }

            return elevationRequest;
        }
    }

    internal class PortInformationData
    {
        public string Name { get; }

        public int Port { get; }

        public int ProcessId { get; }

        [JsonConstructor]
        public PortInformationData(string name, int port, int processId)
        {
            this.Name = name;
            this.Port = port;
            this.ProcessId = processId;
        }

        public PortInformationData(ServicePortMapping servicePortMapping)
        {
            this.Name = servicePortMapping.ServiceName;
            this.Port = servicePortMapping.PortNumber;
            this.ProcessId = servicePortMapping.ProcessId;
        }

        public PortInformationData(ProcessPortMapping processPortMapping)
        {
            this.Name = processPortMapping.ProcessName;
            this.Port = processPortMapping.PortNumber;
            this.ProcessId = processPortMapping.ProcessId;
        }
    }
}