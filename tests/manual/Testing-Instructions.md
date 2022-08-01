# Testing Instructions

**Table of Contents**
- [Priorities](#priorities)
- [Schedule](#schedule)
- [Instructions](#instructions)
- [Regression Testing](#regression-testing)
- [Reporting](#reporting)
- [Production Clients](#production-clients)
- [Staging Clients](#staging-clients)
- [Dev Clients](#dev-clients)

# <b id="priorities">Priorities</b>

Follow these priorities when deciding which environment to test:
1. If new [Production Clients](#production-clients) (CLIs and VSIXs) have been released, test the primary scenario with them.
2. If new [Staging Clients](#staging-clients) have been released, test the primary scenario with them.
3. New builds of the [Dev Client](#dev-clients) are released daily and should be tested regularly to verify functionality.
4. Ensure that fixed bugs stay fixed via [Regression Testing](#regression-testing).

# <b id="schedule">Schedule</b>

|   | Monday | Tuesday | Wednesday | Thursday | Friday |
| --- | --- | --- | --- | --- | --- | 
| OS | Windows| Linux | Windows |Mac |Windows |
| Environment | Dev | Dev<br>Prod | Staging| Dev<br>Prod | Prod |
| Scenarios | [Bridge](./BridgeToKubernetes.md#bridge-routing-vscode) | On even days: Group A<br>On odd days: Group B | [Bridge](./BridgeToKubernetes.md) | On even days: Group A<br>On odd days: Group B | [Bridge](./BridgeToKubernetes.md) |

## Scenario groups
|   | Group A | Group B |
| --- | --- | --- | 
| VSCode | * Bridge non-isolated<br>* Bridge isolated<br>* KubernetesLocalProcessConfig.yaml<br>* External Endpoints| * Service Env vars<br>* Pod Identity<br>* Kubernetes Extension integration<br>* Dapr<br>* Https Ingresses |


# <b id="Instructions"> Instructions</b>

1. Set the BRIDGE_ENVIRONMENT environment variable.
    * Production: `BRIDGE_ENVIRONMENT=prod` (or don't set it at all)
    * Staging: `BRIDGE_ENVIRONMENT=staging`
    * Development: `BRIDGE_ENVIRONMENT=dev`

2. If you are changing environments in VSCode from dev to staging, dev to prod, or from staging to prod, remember to delete everything inside of the "file-downloader-downloads". These can be found at:
    * On Windows: %UserProfile%\AppData\Roaming\Code\User\globalStorage\mindaro.mindaro\file-downloader-downloads\
    * On MacOS: /Users/your_username/Library/"Application Support"/Code/User/globalStorage/mindaro.mindaro/file-downloader-downloads/
    * On Linux: /home/your_username/.config/Code/User/globalStorage/mindaro.mindaro/file-downloader-downloads/

3. Create an RBAC-enabled cluster in one of these regions using the Kubernetes version specified in the schedule.
    * CanadaEast
    * SoutheastAsia
    * WestEurope
    * EastUS

4. Follow these instructions to test the current Bridge to Kubernetes feature: https://mindaromaster.blob.core.windows.net/vscode/CTI/BridgeToKubernetes.md.

5. Report the results. Follow the [Reporting](#reporting) section for more information.

# <b id="regression-testing">Regression Testing</b>
Attempt to reproduce old fixed bugs in rotating environments and platforms to ensure that the fixes don't regress. Focus on bugs with high customer impact that wouldn't be caught during normal testing.

# <b id="reporting">Reporting</b>
When reporting testing results for the CLI, please include the following information using this [bug template](TBD need new git bug template).

* CLI/VSIX Environment (Prod, Pre-Prod, Staging, or Dev)
* CLI/VSIX build number
* Platform (Client Operating System)
* CLuster region
* Kubernetes version
* VS version
* VS Code version
* Success or failure of the command
* Screenshots of the issue
* Log files from the DSC CLI, Bridge to Kubernetes Library, and EndpointManager (from %TEMP%\Bridge to Kubernetes)

**Specific instructions for EndpointManager:**
1. Always include `bridge-endpointmanager-<date>.txt` logs from `%TEMP%\Bridge to Kubernetes` when testing Bridge to Kubernetes.
2. If there are no logs for this and the Connect operation fails, please try to start the DNS manager manually from a command prompt to verify the DNS manager works. 
    - The output should have several `TRACE` logs ending with `"Local EndpointManager started on port 50052"` .
    - If the outpute shows an `ERROR` instead, please log a bug with a screenshot and log files.
3. For VS Code, it should live in the same folder as the DSC CLI.
    - Windows: `C:\Users\<username>\AppData\Roaming\Code\User\globalStorage\mindaro.mindaro\file-downloader-downloads\binaries`
    - Mac: `/Users/<username>/Library/Application Support/Code/User/globalStorage/mindaro.mindaro/file-downloader-downloads`
4. For VS, you can search for it under `C:\Program Files (x86)\Microsoft Visual Studio\2019\Preview\Common7\IDE\Extensions`.

# <b id="production-clients">Production Clients</b>
| Prod Client | Download Link |
| --- | --- |
| Bridge to Kubernetes VSCode VSIX | https://marketplace.visualstudio.com/items?itemName=mindaro.mindaro |
| VS | Install the Azure Workload on VS 2019 setup |


# <b id="staging-clients">Staging Clients</b>
| Staging Client | Download Link |
| --- | --- |
| Bridge to Kubernetes VSCode VSIX | https://mindarostaging.blob.core.windows.net/vscode/LKS/mindaro-0.1.1.vsix |

# <b id="dev-clients">Dev Clients</b>
| Dev Client | Download Link |
| --- | --- |
| Bridge to Kubernetes VSCode VSIX | https://mindaromaster.blob.core.windows.net/vscode/LKS/mindaro-0.1.1.vsix |

To download a specific build, replace LKS in the URL with a build number.