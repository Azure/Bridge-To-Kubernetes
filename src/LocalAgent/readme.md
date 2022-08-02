Introduction
------------
 This project contains code for localagent service. The localagent is meant to be used as sidecar when running Bridge with the user workload running containerized.


Building
-------------
First you need to create VSTS/Azure devops pat token to access Devdiv private nuget feed. [Create DevDiv PAT.](https://devdiv.visualstudio.com/_usersSettings/tokens)

You just need to select the *Read* privilege under *Packaging*

To build docker image from repo root folder, run `docker build -f src\localagent\Dockerfile --build-arg DEVDIV_PKGS_USERNAME=<your @microsoft id, eg: lolodi@microsoft.com> --build-arg DEVDIV_PKGS_PASSWORD=<PAT token> .`

