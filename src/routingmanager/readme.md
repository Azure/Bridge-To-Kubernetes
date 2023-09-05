 # routingmanager

 Introduction
 ------------
 Routing Manager, referred to as RM from this point onwards, is a microservice that runs on the user cluster and continuously watches certain kubernetes objects in a particular namespace:
 1. It watches all ingress objects in the namespace and makes a note of all services mentioned in these ingresses
 2. It watches all service objects with the label routing.visualstudio.io/route-from and annotation routing.visualstudio.io/route-on-header

 For each of the above services "S" it finds, the RM
 1. Creates a new service   "S_clone", cloned from "S" with the same label selector as "S" such that both "S" and "S'" point to the same pod(s).
 2. Deploys an [envoy](https://www.envoyproxy.io/) pod per service "S" and configures its rules.
 3. Change the label selector of "S" such that it now points over the envoy pod.

 RM also exposes an http endpoint to return the "status" of the service.

 
Includes basic support for IngressRoute CRD by Traefik - https://github.com/projectcontour/contour/blob/main/design/ingressroute-design.md
App used for testing: 
Deployment yamls: https://github.com/codeaprendiz/kubernetes-kitchen/tree/master/gcp/task-005-traefik-whoami
Code: https://github.com/traefik/whoami
    Slight modification to Dockerfile - Use base image as busybox (or any other alpine/go image) instead of scratch because of below bug
    https://devdiv.visualstudio.com/DevDiv/_boards/board/t/Mindaro/Stories/?workitem=1327037

 Developing
------------
 Requires Dotnet core SDK 6.0.

 Experimentation using ExP
 -------------------------
 To flight an experimentation in the Routing manager using ExP, please rely on feature flags being passed as an annotation on devhostagent pod. They can be accessed by Extensions.GetFeatureFlags().
 There are 4 sets of changes to be made once you decide on the feature flag name:
 1. Updates to routing manager for the feature flag.
 2. VSCode Extension changes where you would need to hard-code the feature flag name to check if it is enabled or not and to pass it to the connect command. Please refer to the comment in ExtensionRoot.ts : "Todo Use the below block of code to enable a feature flag for routing manager"
 3. Updates to //exp to add the feature flag to the experiment.
 4. Release a new version of the VSCode extension since we do not download a new version of binaries unless a new VSCode extension version.

 Building
-------------
Under routing manager folder run:
 dotnet build

Running/Debugging
-------
 

Testing
-------
To test your routing manager changes, you will need to build & push your own image.  Cd into the "Mindaro-Connect" folder that contains your changes and run the following:
Note: your registry has to be public (docker hub works fine)

`docker build -f ./src/routingmanager/Dockerfile -t <your registry>/<your imagename, e.g. michelleroutingmanager> .`

Follow this by:
`docker push <your registry>/<your imagename>`

Steps to debug routing manager custom image using vscode:

1. Open the Mindaro Connect src folder in VsCode (code Bridge-To-Kubernetes/src), please note the following will not work if you are inside dsc or routing manager folder.
2. Create a launch.json using the following json and set the BRIDGE_ROUTINGMANAGERIMAGENAME variable to be your custom image.
`{
    "version": "0.2.0",
    "configurations": [
        {
            // Use IntelliSense to find out which attributes exist for C# debugging
            // Use hover for the description of the existing attributes
            // For further information visit https://github.com/OmniSharp/omnisharp-vscode/blob/master/debugger-launchjson.md
            "name": "bridge",
            "type": "coreclr",
            "request": "launch",
            // "preLaunchTask": "BuildAndPublishMac", // Replace by BuildAndPublishWindows as needed
            // If you have changed target frameworks, make sure to update the program path.
            // Replace osx-x64 with win-x64 if you are running in windows
            "program": "${workspaceFolder}/dsc/bin/Debug/net7.0/linux-x64/dsc.dll",
            "args": ["connect", "--service", "stats-api", "--local-port", "3001", "--use-kubernetes-service-environment-variables", "--routing", "custom-routing-name", "--namespace", "todo-app"],
            "env": {
                "BRIDGE_ENVIRONMENT":"dev",
                "BRIDGE_BINARYUTILITYVERSION":"v1",
                "BRIDGE_ROUTINGMANAGERIMAGENAME":"yourregistry/imagename:version"
            },
            "cwd": "${workspaceFolder}/dsc",
            // For more information about the 'console' field, see https://aka.ms/VSCode-CS-LaunchJson-Console
            "console": "internalConsole",
            "stopAtEntry": false
        }
    ]
}`
Run a Bridge debug session from vscode debug menu.
When you see your routing pod come up, run `kubectl describe` to make sure it is running your custom image.


Troubleshoot
------------


Release Management
------------------
TBD new pipelines coming in new github repo