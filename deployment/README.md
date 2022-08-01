# Release Process

These instructions describe how to take changes from the main branch and move them through the various processes and pipelines to release them to customers.

## Contents
- [Build and Release pipelines](#build-and-release-pipelines)
- [Dev](#dev)
- [Staging](#staging)
    - [Preparation](#staging-preparation)
    - [Snap main into the staging branch](#snap-main-into-the-staging-branch)
    - [Release](#staging-release)
- [Production](#production)
    - [Preparation](#production-preparation)
    - [Snap staging into the production branch](#snap-staging-into-the-production-branch)
    - [Release](#production-release)
    - [Hotfix](#hotfix)
- [Appendix](#appendix)
    - [Analysis and sanity builds](#analysis-and-sanity-builds)
    - [Prerequisites to snap](#prerequisites-to-snap)
    - [Notes](#notes)

## Build and Release pipelines

| Build | Release | Component |
| ----- | ------- | --------- |
| TBD | TBD on new git pipelines | TBD on new git pipelines
# Dev
Whenever a change is merged into the main branch, the [Build and release pipelines](#build-and-release-pipelines) produce updated components and automatically release them into the Dev environment. No manual intervention is needed unless a pipeline fails.

| Component | Released to | Download |
| --------- | ----------- | -------- |
| Service images | [mindarodev](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/20c6254d-ab44-45c8-8885-ceb54699e1bf/resourceGroups/Dev/providers/Microsoft.ContainerRegistry/registries/mindarodev/repository) Azure Container Registry | `mindarodev.azurecr.io/routingmanager:latest`<br>`mindarodev.azurecr.io/lpkremoteagent:latest`<br>`mindarodev.azurecr.io/lpkrestorationjob:latest` | 
| .zip file | [mindaromaster](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/20c6254d-ab44-45c8-8885-ceb54699e1bf/resourceGroups/Dev/providers/Microsoft.Storage/storageAccounts/mindaromaster/containersList) Storage Account | https://mindaromaster.blob.core.windows.net/zip/lks.json (contains .zip link) |
| NuGet package | [Mindaro-Dev](https://dev.azure.com/DevDiv/DevDiv/_packaging?_a=feed&feed=Mindaro-Dev) NuGet feed | [Microsoft.DevSpaces.CLI](https://dev.azure.com/DevDiv/DevDiv/_packaging?_a=package&feed=Mindaro-Dev&package=Microsoft.DevSpaces.CLI&protocolType=NuGet) |
| VSCode extension | [mindaromaster](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/20c6254d-ab44-45c8-8885-ceb54699e1bf/resourceGroups/Dev/providers/Microsoft.Storage/storageAccounts/mindaromaster/containersList) Storage Account | https://mindaromaster.blob.core.windows.net/vscode/LKS/mindaro-1.0.1.vsix |


# Staging

<a id="staging-preparation"></a>
## Preparation

TBD need git replacement

<a id="staging-release"></a>
## Release

Whenever changes are pushed to the staging branch, the [Build and release pipelines](#build-and-release-pipelines) build updated components and automatically release them into the Staging environment. No manual intervention is needed unless a pipeline fails.

After the releases complete, test to ensure that the new staging version of the VSCode extension can successfully download the correct staging build of client binaries (`set BRIDGE_ENVIRONMENT=staging`).

| Component | Released to | Download |
| --------- | ----------- | -------- |
| Service images | [mindarostage](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/20c6254d-ab44-45c8-8885-ceb54699e1bf/resourceGroups/Staging/providers/Microsoft.ContainerRegistry/registries/mindarostage/repository) Azure Container Registry | `mindarostage.azurecr.io/routingmanager:latest`<br>`mindarostage.azurecr.io/lpkremoteagent:latest`<br>`mindarostage.azurecr.io/lpkrestorationjob:latest` | 
| .zip file | [mindarostaging](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/20c6254d-ab44-45c8-8885-ceb54699e1bf/resourceGroups/Staging/providers/Microsoft.Storage/storageAccounts/mindarostaging/containersList) Storage Account | https://mindarostaging.blob.core.windows.net/zip/lks.json (contains .zip link) |
| VSCode extension | [mindarostaging](https://ms.portal.azure.com/#@microsoft.onmicrosoft.com/resource/subscriptions/20c6254d-ab44-45c8-8885-ceb54699e1bf/resourceGroups/Staging/providers/Microsoft.Storage/storageAccounts/mindarostaging/containersList) Storage Account | https://mindarostaging.blob.core.windows.net/vscode/LKS/mindaro-1.0.1.vsix |


# Production

<a id="production-preparation"></a>
## Preparation

TBD need release plan for git
## Snap staging into the production branch
TBD need plan for git

<a id="production-release"></a>
## Release

Whenever changes are pushed to the production branch, the [Build and release pipelines](#build-and-release-pipelines) build updated components and create releases to deploy the changes them into the Production environment. **The 'Prod' stage of each release must be manually approved.**

After the releases complete, test to ensure that the new version of the VSCode extension installs and successfully downloads the new build of client binaries.

The team that owns the VS extension will receive a notification when a new version of the NuGet package is pushed, but it's a good idea to contanct them when a new a new package is available and coordinate its integration and release. The dev owner is David Negstad (danegsta@microsoft.com) and lead is Devin Breshears (devinb@microsoft.com).

| Release | Component | Released to | Download |
| ------- | --------- | ----------- | -------- |
| [Mindaro-Connect-Services](TBD need git replacement) | Service images | [bridgetokubernetes](https://ms.portal.azure.com/#@MSAzureCloud.onmicrosoft.com/resource/subscriptions/4338e903-7fae-4c64-ab95-310ceb678ddf/resourceGroups/Bridge/providers/Microsoft.ContainerRegistry/registries/bridgetokubernetes/repository) Azure Container Registry | `bridgetokubernetes.azurecr.io/routingmanager:latest`<br>`bridgetokubernetes.azurecr.io/lpkremoteagent:latest`<br>`bridgetokubernetes.azurecr.io/lpkrestorationjob:latest` | 
| [Mindaro-Connect-Zip](https://dev.azure.com/DevDiv/DevDiv/_release?_a=releases&view=mine&definitionId=2564) | .zip file | [bridgetokubernetes](https://ms.portal.azure.com/#@MSAzureCloud.onmicrosoft.com/resource/subscriptions/4338e903-7fae-4c64-ab95-310ceb678ddf/resourceGroups/Bridge/providers/Microsoft.Storage/storageAccounts/bridgetokubernetes/containersList) Storage Account | https://bridgetokubernetes.blob.core.windows.net/zip/lks.json (contains .zip link) |
|
| [Mindaro-Connect-VSCode](TBD) | VSCode extension | Visual Studio Marketplace | https://marketplace.visualstudio.com/items?itemName=mindaro.mindaro |

## Hotfix

If an urgent issue is found in production, a targeted hotfix can be released without snapping branches or re-releasing all components.
1. Test the fix in the Dev and/or Staging environments to ensure it resolves the issue and doesn't introduce any new problems
1. Cherry-pick the change from the main or staging into the production branch
1. If the fix is in the client binaries:
    - Consider updating the `CliMinVersion` in [Constants.ts](../src/vscode/src/Constants.ts) to ensure that VSCode users get the new client binaries when their extension is updated
    - Also consider releasing a new NuGet package so the team that owns the VS extension can integrate the fix and release an update. Contact David Negstad (danegsta@microsoft.com) and Devin Breshears (devinb@microsoft.com) on the VS team to coordinate.
1. Approve Prod releases of only the impacted component(s) and ignore (or cancel) the others. If the change was confined to services (e.g. devhostagent, restorationjob, or routingmanager), no release is necessary because the image will be pushed by the [Mindaro-Connect-Services](https://dev.azure.com/DevDiv/DevDiv/_build?definitionId=13124) build.


# Appendix

## Analysis and sanity builds
Confirm that these builds are completing successfully on the source branch before snapping, and on the target branch after snapping.
- [Mindaro-Connect-SecDevTools](TBD need git replacement)
- [Mindaro-Connect-SDL-Scan](TBD need git replacement)
- [Mindaro-Connect-SanityBuild](TBD need git replacement)
- [Mindaro-Connect-VSCode-SanityBuild](TBD need git replacement)

## Prerequisites to snap

To force push (`git push -f`) into the staging and production branches, you'll need to define permissions for yourself on both. If you don't have permissions to define your permissions, Dan (daniv@microsoft.com) can grant you the necessary permissions.
1. Open https://dev.azure.com/DevDiv/DevDiv/_git/Mindaro-Connect/branches
1. On both the staging and production branches, open '...' > 'Branch security' and add an entry for your alias with `Allow` permissions for all actions
    - Note that your permissions to 'Bypass policies when pushing' and 'Force push' will be overridden to `Deny` by the Mindaro team policy EXCEPT when you temporarily relax Mindaro permissions during a snap

## Notes
- We use `git reset` to snap into staging and production rather than `git merge` because over time with hotfixes, manual merge conflict resolution, and cherry-picking the branches tend to drift apart unless they're aligned with `git reset`. By resetting for every snap, there can be no unexpected differences lurking in the production branch.
