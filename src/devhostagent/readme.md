 Introduction
-------------
devhostagent is a process running inside user's container, it can run either as a startup action, or started separately. devhostAgent serves 2 purposes:

1. devhostagent can launch process inside the user's container. For example, for dotnet launch debugging scenario, devhostagent is set as the container's startup command. Once the container is started, the CLI will instruct devhostagent to launch the debugger or the target process as needed. The output from can be streamed back to the client side.
2. While debugging with VSCode or Visual studio, for every run of the F5 we wouldn't completely **up** the service if there were no code changes that require rebuilding the project. We will synchronize the source code change into the user's container via devhostagent, and then use devhostagent to launch the compiler inside the user's container.

This tool is built into **azds**/devhostagent:**tag** image and mounted as an import via azds. A container with this image is deployed in the user's cluster through initializer.

Building
---------
To make changes to this tool, follow these steps:  
1. On your local machine, run these from src\devhostagent\ directory to build the container images:
   docker build -f ./src/devhostagent/Dockerfile -t **yourDockerHub**/**devhostimageName**:**yourTag** .  
   docker build -t **yourDockerHub**/**devhostimageName**:**yourTag** . 
      or
   docker build -t **yourDockerHub**/**devhostimageName**:**yourTag** --build-arg Configuration=Debug .
   and push it  
   docker push **yourDockerHub**/**devhostimageName**:**yourTag**  

Running/Debugging
-------
After building your own devhostagent image, follow these steps to test it:
1. Set environment variable BRIDGE_DEVHOSTIMAGENAME to the image name/tag you just built. ImageProvider.cs will use this value to override the default devhostagent image.
2. 'azds down' to remove current running instance.
3. In the next 'azds up' or full debug deployment, devhostagent will be updated. Please note to shutdown VSCode or VS so that it will pick up the environment variable changes. 

Add tracing to help with debugging.

Versioning
----------
If you made impactful changes to the devhostagent project, then you need to increment the tag for the image before merging your changes into master.

Why should we update the version? Because a user might create a new Controller on a Cluster that previously was used for an older Controller. In that case the nodes might already have an old devhostagent image, and an updated image won't be pulled from the repository unless a new image has been published with an updated version tag.

Increment the MINDARO_DEVHOSTAGENT_TAG value in deployment\settings\services\imagetag.setting: MINDARO_DEVHOSTAGENT_TAG=0.1.**24**

The image tag will be pulled into common as an embedded resource when built.

Troubleshoot
------------
Examine the output of devhostagent. 

Release Management
------------------
TBD need new git pipelines - this was under services in vsts
