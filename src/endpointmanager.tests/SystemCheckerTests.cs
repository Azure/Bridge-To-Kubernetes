// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Linq;
using Microsoft.BridgeToKubernetes.TestHelpers;
using Xunit;

namespace Microsoft.BridgeToKubernetes.EndpointManager.Tests
{
    public class SystemCheckerTests : TestsBase
    {
        private readonly WindowsSystemCheckService _windowsSystemCheckService;

        public SystemCheckerTests()
        {
            _windowsSystemCheckService = _autoFake.Resolve<WindowsSystemCheckService>();
        }

        [Fact]
        public void WindowCheckerParseProcessPortMap()
        {
            var result = _windowsSystemCheckService.ParseProcessPortMap(Netstat_Out_1);
            Assert.True(result.ContainsKey(80), "0.0.0.0:80 not identified!");
            Assert.Equal("PID 14688 [com.docker.backend.exe]", result[80]);

            Assert.True(result.ContainsKey(135), "0.0.0.0:135 not identified!");
            Assert.Equal("PID 1272 RpcSs [svchost.exe]", result[135]);

            Assert.True(result.ContainsKey(445), "0.0.0.0:445 not identified!");
            Assert.Equal("PID 4 Cannot obtain ownership information", result[445]);

            Assert.True(result.ContainsKey(3389), "0.0.0.0:3389 not identified!");
            Assert.Equal("PID 1652 TermService [svchost.exe]", result[3389]);
        }

        [Fact]
        public void WindowCheckerParseProcessPortMapEmpty()
        {
            var result = _windowsSystemCheckService.ParseProcessPortMap(string.Empty);
            Assert.True(result.Count == 0);
        }

        [Fact]
        public void WindowsCheckerKnownServices()
        {
            var result = _windowsSystemCheckService.ParseForKnownService(NetStart_Out_1);
            Assert.NotEmpty(result);

            bool branchCacheFound = false;
            foreach (var m in result)
            {
                if (m.Message.Contains("Service 'BranchCache' is taking common port(s) '80' on your machine"))
                {
                    branchCacheFound = true;
                }
            }
            Assert.True(branchCacheFound);
        }

        [Fact]
        public void WindowsCheckerKnownServicesEmpty()
        {
            var result = _windowsSystemCheckService.ParseForKnownService(string.Empty).ToArray();
            Assert.True(result.Length == 0);
        }

        private const string Netstat_Out_1 = @"

Active Connections

  Proto  Local Address          Foreign Address        State           PID
  TCP    0.0.0.0:80             0.0.0.0:0              LISTENING       14688
 [com.docker.backend.exe]
  TCP    0.0.0.0:135            0.0.0.0:0              LISTENING       1272
  RpcSs
 [svchost.exe]
  TCP    0.0.0.0:445            0.0.0.0:0              LISTENING       4
 Cannot obtain ownership information
  TCP    0.0.0.0:623            0.0.0.0:0              LISTENING       16312
 [LMS.exe]
  TCP    0.0.0.0:2179           0.0.0.0:0              LISTENING       5588
 [vmms.exe]
  TCP    0.0.0.0:3389           0.0.0.0:0              LISTENING       1652
  TermService
 [svchost.exe]
  TCP    0.0.0.0:5040           0.0.0.0:0              LISTENING       6712
  CDPSvc
 [svchost.exe]
  TCP    0.0.0.0:5357           0.0.0.0:0              LISTENING       4
 Cannot obtain ownership information
  TCP    0.0.0.0:7680           0.0.0.0:0              LISTENING       4216
 Cannot obtain ownership information
  TCP    0.0.0.0:16992          0.0.0.0:0              LISTENING       16312
 [LMS.exe]
  TCP    0.0.0.0:49664          0.0.0.0:0              LISTENING       1036
 [lsass.exe]
  TCP    0.0.0.0:49665          0.0.0.0:0              LISTENING       116
 Cannot obtain ownership information
  TCP    0.0.0.0:49666          0.0.0.0:0              LISTENING       2012
  EventLog
 [svchost.exe]
  TCP    0.0.0.0:49667          0.0.0.0:0              LISTENING       2052
  Schedule
 [svchost.exe]
  TCP    0.0.0.0:55900          0.0.0.0:0              LISTENING       3476
  SessionEnv
 [svchost.exe]
  TCP    0.0.0.0:55906          0.0.0.0:0              LISTENING       4700
 [spoolsv.exe]
  TCP    0.0.0.0:55924          0.0.0.0:0              LISTENING       960
 Cannot obtain ownership information
  TCP    10.0.75.1:139          0.0.0.0:0              LISTENING       4
 Cannot obtain ownership information
  TCP    10.127.70.99:139       0.0.0.0:0              LISTENING       4
 Cannot obtain ownership information
  TCP    10.127.70.99:7680      10.127.70.55:55607     ESTABLISHED     4216
 Cannot obtain ownership information
  TCP    10.127.70.99:49167     104.46.116.119:443     ESTABLISHED     8472
  CDPUserSvc_a4d17
 [svchost.exe]
  TCP    10.127.70.99:49169     13.78.184.186:443      ESTABLISHED     4216
 Cannot obtain ownership information
  TCP    10.127.70.99:49170     52.114.128.8:443       TIME_WAIT       0
  TCP    10.127.70.99:49171     10.60.255.30:443       TIME_WAIT       0
  TCP    10.127.70.99:49172     10.60.255.30:443       ESTABLISHED     14820
 [CcmExec.exe]
  TCP    10.127.70.99:49180     52.114.132.74:443      TIME_WAIT       0
  TCP    10.127.70.99:49199     13.64.188.245:443      ESTABLISHED     4436
 Cannot obtain ownership information
  TCP    10.127.70.99:49220     40.90.137.127:443      ESTABLISHED     29372
  wlidsvc
 [svchost.exe]
  TCP    10.127.70.99:49222     10.89.30.117:7680      SYN_SENT        4216
 Cannot obtain ownership information
  TCP    10.127.70.99:51807     10.159.66.9:135        ESTABLISHED     4700
 [spoolsv.exe]
  TCP    10.127.70.99:51808     10.159.66.9:59855      ESTABLISHED     4700
 [spoolsv.exe]
  TCP    10.127.70.99:51810     10.159.66.9:59855      ESTABLISHED     4700
 [spoolsv.exe]
  TCP    10.127.70.99:52410     52.230.222.68:443      ESTABLISHED     13416
 [OneDrive.exe]
  TCP    10.127.70.99:53806     52.114.128.36:443      ESTABLISHED     14068
 [Teams.exe]
  TCP    10.127.70.99:53808     52.141.217.125:443     ESTABLISHED     13352
 [OneDrive.exe]
  TCP    10.127.70.99:53814     52.114.142.156:443     ESTABLISHED     14068
 [Teams.exe]
  TCP    10.127.70.99:55914     52.230.222.68:443      ESTABLISHED     5072
  WpnService
 [svchost.exe]
  TCP    10.127.70.99:61279     64.4.54.254:443        ESTABLISHED     19320
 [PerfWatson2.exe]
  TCP    10.127.70.99:61764     10.88.188.129:7680     FIN_WAIT_1      4216
 Cannot obtain ownership information
  TCP    10.127.70.99:64683     40.84.185.67:9354      ESTABLISHED     19176
 [ServiceHub.SettingsHost.exe]
  TCP    10.127.70.99:65377     10.127.68.87:22        ESTABLISHED     5424
 [ssh.exe]
  TCP    10.127.70.99:65441     10.60.255.30:443       TIME_WAIT       0
  TCP    127.0.0.1:6443         0.0.0.0:0              LISTENING       14688
 [com.docker.backend.exe]
  TCP    127.0.0.1:6443         127.0.0.1:64497        ESTABLISHED     14688
 [com.docker.backend.exe]
  TCP    127.0.0.1:50659        0.0.0.0:0              LISTENING       15492
 [com.docker.proxy.exe]
  TCP    127.0.0.1:50898        0.0.0.0:0              LISTENING       16312
 [LMS.exe]
  TCP    127.0.0.1:50899        127.0.0.1:50900        ESTABLISHED     16312
 [LMS.exe]
  TCP    127.0.0.1:50900        127.0.0.1:50899        ESTABLISHED     16312
 [LMS.exe]
  TCP    127.0.0.1:64497        127.0.0.1:6443         ESTABLISHED     15492
 [com.docker.proxy.exe]
  TCP    127.0.0.1:64505        0.0.0.0:0              LISTENING       17964
 [SCNotification.exe]
  TCP    127.0.0.1:64707        0.0.0.0:0              LISTENING       15916
 [devenv.exe]
  TCP    127.0.0.1:64708        0.0.0.0:0              LISTENING       4808
 [Microsoft.Alm.Shared.Remoting.RemoteContainer.dll]
  TCP    127.0.1.1:80           0.0.0.0:0              LISTENING       4676
  iphlpsvc
 [svchost.exe]
  TCP    192.168.95.145:139     0.0.0.0:0              LISTENING       4
 Cannot obtain ownership information
";

        private const string NetStart_Out_1 = @"These Windows services are started:

   Application Host Helper Service
   Application Identity
   Application Information
   AVCTP service
   Background Intelligent Transfer Service
   Background Tasks Infrastructure Service
   Base Filtering Engine
   BitLocker Drive Encryption Service
   BitLocker Management Client Service
   BranchCache
   Certificate Propagation
   Client License Service (ClipSVC)
   Clipboard User Service_c3519
   CNG Key Isolation
   COM+ Event System
   Connected Devices Platform Service
   Connected Devices Platform User Service_c3519
   Connected User Experiences and Telemetry
   Contact Data_c3519
   CoreMessaging
   Credential Manager
   Cryptographic Services
   Data Sharing Service
   Data Usage
   DCOM Server Process Launcher
   Delivery Optimization
   DHCP Client
   Diagnostic Policy Service
   Diagnostic Service Host
   Display Policy Service
   Distributed Link Tracking Client
   DNS Client
   Extensible Authentication Protocol
   Geolocation Service
   Host Network Service
   HV Host Service
   Hyper-V Host Compute Service
   Hyper-V Virtual Machine Management
   IKE and AuthIP IPsec Keying Modules
   Internet Connection Sharing (ICS)
   IP Helper
   Local Session Manager
   LxssManagerUser_c3519
   Microsoft Account Sign-in Assistant
   Microsoft Office Click-to-Run Service
   Microsoft Online Services Sign-in Assistant
   Microsoft Store Install Service
   Microsoft TelemetryHost Service
   Microsoft Windows SMS Router Service.
   Netlogon
   Network Connection Broker
   Network List Service
   Network Location Awareness
   Network Store Interface Service
   Network Virtualization Service
   NVIDIA Display Driver Service
   Offline Files
   Plug and Play
   Power
   Print Spooler
   Program Compatibility Assistant Service
   Remote Access Connection Manager
   Remote Desktop Configuration
   Remote Desktop Services
   Remote Desktop Services UserMode Port Redirector
   Remote Procedure Call (RPC)
   RPC Endpoint Mapper
   Secure Socket Tunneling Protocol Service
   Security Accounts Manager
   Security Center
   Server
   Shell Hardware Detection
   Smart Card Device Enumeration Service
   SMS Agent Host
   SQL Server VSS Writer
   SSDP Discovery
   State Repository Service
   Storage Service
   Sync Host_c3519
   SysMain
   System Event Notification Service
   System Events Broker
   System Guard Runtime Monitor Broker
   Task Scheduler
   TCP/IP NetBIOS Helper
   Themes
   Time Broker
   Touch Keyboard and Handwriting Panel Service
   Update Orchestrator Service
   User Data Access_c3519
   User Data Storage_c3519
   User Manager
   User Profile Service
   Web Account Manager
   Windows Audio
   Windows Audio Endpoint Builder
   Windows Biometric Service
   Windows Connection Manager
   Windows Defender Advanced Threat Protection Service
   Windows Defender Antivirus Network Inspection Service
   Windows Defender Antivirus Service
   Windows Defender Firewall
   Windows Event Log
   Windows Font Cache Service
   Windows License Manager Service
   Windows Management Instrumentation
   Windows Process Activation Service
   Windows Push Notifications System Service
   Windows Push Notifications User Service_c3519
   Windows Remote Management (WS-Management)
   Windows Search
   Windows Security Service
   Windows Time
   Windows Update
   WinHTTP Web Proxy Auto-Discovery Service
   Wired AutoConfig
   Workstation
   World Wide Web Publishing Service
   Xbox Live Auth Manager

The command completed successfully.

";
    }
}