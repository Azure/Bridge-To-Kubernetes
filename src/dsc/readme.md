# DSC.EXE

 Introduction
 ------------
 This project produces a command line executable (dsc.exe) that drives the Library for connecting your local machine to a Kubernetes cluster.
 DSC stands for "Dev Spaces Connect", that was a very early name of the Bridge feature. Renaming it to "bridge.exe" created considerable signing issues on MacOS, so we kept dsc.exe.

Prerequisites
-------------
If you didn't install it already, don't forget to install the Artifact Credential Provider (more info in /src/nugetreadme.md).
You'll also need to have .NET 7.0 SDK and Runtime installed on your machine.
Finally, before trying to build dsc.exe specifically, you should try to build the whole solution to make sure everything builds as expected:
- Open src/all.sln in Visual Studio 2019 or 2022
- Run Build > Rebuild Solution
If you get any errors at this step, we need to fix them before trying to build specifically dsc.exe.

 Developing new CLI Commands
----------------------------
Before developing it might be useful to understand how the CLI works at the Console Application framework level.
Unfortunately we are using a no longer supported framework that uses a less than ideal design to organize commands into a CommandLineApplication. We have built our own abstraction on top of it to simplify things but unfortunately there are still some weird patterns going on.

The root of the application is the `CliApp`. This gets resolved in Program.cs and executed right away.
When CliApp is resolved, all of its constructor's dependencies are resolved as well. Between those we'll focus on the `commandLineArgumentsManager` and the `commandsConfigurator`.

The `commandLineArgumentsManager` is in charge of parsing the verbosity argument (if present) and separating potential CommandExecuteArguments that are used when executing optional commands that are not used by our CLI command, but are instead passed through to some dependency of the CLI command. This parsing happens when the class is activated the first time, so by the time the `CliApp` gets it, all the arguments have already being processed and should be accessed through the `commandLineArgumentsManager`.

The `commandsConfigurator` is in charge of configuring every command.
Each command has to expose a `Configure` method that configures the command adding its available options. In the `Configure` method we always define an `OnExecute` delegate which will be executed at a later time if that particular command is the one to run. The `OnExecute` delegate is used to process and validate the values of the command line arguments to be used during command execution.
The `commandsConfigurator.ConfigureCommands()` relies on the `RootCommand` to cycle through every top level command and run its Configure. After that it calls `Execute` on the `commandLineApplication`, this in turn calls the `OnExecute` on only the command that has to be run.
Each Command also has an `ExecuteAsync`, which is called after the `OnExecute` delegate, to actually run the command.

NOTE: `CliApp` knows on which command to call `ExecuteAsync` because during the `OnExecute` the command to be run called `this.SetCommand`, this sets itself as the Command to be run in the `commandLineArgumentsManager`. This way the `CliApp` can just use `commandLineArgumentsManager.Command` to figure out which is the right command to call.

NOTE: Because the constructor and the `Configure` method of every command is called every time we should try to make them as lightweight as possible to reduce performance impact. Use Lazy initialization when possible.

If you were able to follow all of this, congrats! You can now develop your own commands :)

Building
--------
 To build the project, open a command prompt at the root of the cloned repository (so in `Mindaro-Connect\` folder), and run the following command:  
```
dotnet build src\dsc\dsc.csproj
```

To publish dsc.exe in the form it's consumed by the Bridge to Kubernetes VSCode extension (replace win-x64 with osx-x64 if running on MacOS) : 
```
dotnet publish src\dsc\dsc.csproj -r win-x64
```

To publish a fully self-contained dsc.exe like the one included in the Microsoft.BridgeToKubernetes.CLI NuGet package (for insertion into VS and the CLI), specify the PublishProfile like this:
```
dotnet publish src\dsc\dsc.csproj -r win-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
```

To build the Microsoft.BridgeToKubernetes.CLI NuGet package, publish for all platforms and then run `dotnet pack`:
```
dotnet publish src\dsc\dsc.csproj -r win-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
dotnet publish src\dsc\dsc.csproj -r osx-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
dotnet publish src\dsc\dsc.csproj -r linux-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
dotnet pack src\dsc\dsc.csproj -p:NuspecFile=exe.nuspec
```
Running/Debugging
-----------------
**Running:** Publish the `src\dsc\dsc.csproj` project (replace win-x64 with osx-x64 if running on MacOS) and run the `dsc` executable to test the your changes.  
```
dotnet publish src\dsc\dsc.csproj -r win-x64
```
 
**TIP:** Add `src\dsc\bin\Debug\net7.0\win-x64\publish` to your system's PATH environment variable to run the `dsc` from any directory on the command prompt.  

**Debugging:** 

- In Visual Studio, open `src\client.sln` solution and right click the `dsc.csproj` set the project as startup project and then `properties` > `Debug` tab. In this tab add appropriate values for `arguments`, `Working Directory` and `Environment variables` fields and press F5 to start dsc executable from VS which allows you add breakpoints.
For example, you can set as Application arguments the value `prep-connect --output json --service stats-api` (update the `--service` parameter depending of the actual name of a service you have available in the cluster currently targeted by your kubeconfig), and put a breakpoint in `PrepConnectCommand.cs` at the beginning of the `ExecuteAsync` method, to debug and validate your breakpoint gets caught. This specific command will return a JSON showing the changes we need to perform on the local machine to be able to run this service locally (for example, killing process BranchCache blocking the port 80 and editing the local hosts file).

- In VSCode, make sure you open VSCode on dsc folder. Open launch.json file in ".vscode" folder, it has the program to run (make sure path has proper architecture: win-x64 for windows and osx-64 for MacOS) and the option to update command arguments as needed (command, service, etc as described above). Then go to run and debug section on left panel. "bridge" will be set as default debug job, so just click "Start Debugging" buttom.  

NOTE: When building locally (in VS or `dotnet build`), dsc.csproj won't automatically publish and consume a fresh EndpointManager.exe. Instead, if EndpointManager.proj has already been published, it'll use that copy to avoid the slow process of republishing. If you want to rebuild EndpointManager.exe to pick up new changes, simply run `dotnet clean` in the CLI directory to delete the copy out of the EndpointManager's `publish` directory, then rebuild `dsc.csproj`. Or if you `dotnet publish dsc.csproj`, a fresh EndpointManager.exe will always be created.

Testing
-------
`dsc.tests.csproj` contains the unit test cases for this project.
Run `dotnet test src\dsc.tests\dsc.tests.csproj` to execute all the unit tests for the CLI.

Troubleshoot
------------
< Describe the trouble shooting steps which help to easily identify the problem. It would be helpful to list common error cases. >

Release Management
------------------
TBD - We need to implement github actions/release pipeliens

Managed Identity Flow
---------------------
1. User sets the below in KubernetesLocalProcessConfig.yaml
version: 0.1
enableFeatures:
  - ManagedIdentity

2. User starts debugging with B2K

3. We set hosts file to resolve "managedidentityforbridgetokubernetes" to a local IP

4. Setting MSI_ENDPOINT and MSI_SECRET ensures that all requests to managed identity endpoint are routed to "http://managedidentityforbridgetokubernetes/metadata/identity/oauth2/token"

5. These requests are forwarded to remote agent running on cluster

6. Remote agent intercepts these requests and changes the request slightly so that the request is in correct format to be sent to IMDS endpoint (this is a hack to make use of MSI_ENDPOINT and MSI_SECRET)
Request received by the remote agent is like below:
GET /metadata/identity/oauth2/token?api-version=2017-09-01&resource=https%3A%2F%2Fstorage.azure.com&clientid=<guid> HTTP/1.1
    \nHost: managedidentity
    \nsecret: placeholder
    \nx-ms-client-request-id: e6aadc2a-bb98-4714-bc4c-719676e0d4cf
    \nx-ms-return-client-request-id: true
    \nUser-Agent: azsdk-net-Identity/1.4.0-alpha.20210223.1 (.NET 7.0.1; Microsoft Windows 10.0.19042)
    
We modify it to something like below:
GET /metadata/identity/oauth2/token?api-version=**2018-02-01**&resource=https%3A%2F%2Fstorage.azure.com&**client_id**=<guid> HTTP/1.1
    \nHost: managedidentity
    \nsecret: placeholder
    **\nMetadata: true**
    \nx-ms-client-request-id: 8569c7c4-2411-400f-9d92-5b2e78ec7ec6
    \nx-ms-return-client-request-id: true
    \nUser-Agent: azsdk-net-Identity/1.3.0 (.NET 7.0.1; Microsoft Windows 10.0.19042)

7. Requests are sent to 169.254.169.254

9. Token is fetched and sent back to the local machine.
