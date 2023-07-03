# Endpoint Manager executable

Introduction
------------

Runs on client dev machines and manages the endpoints that local code needs to interact with as if it was running in a Kubernetes cluster. Also helps free up ports by stopping services and process that are running locally and occupying ports needed by the code under development.

Developing
----------
`// TODO:`

Building
--------
To build the project run the following command:
```
dotnet build endpointmanager.csproj
```

To publish EndpointManager.exe in the form it's consumed by the Bridge to Kubernetes VS Code extension: 
```
dotnet publish endpointmanager.csproj -r win-x64
```

To publish a fully self-contained EndpointManager.exe like the one included in the Microsoft.BridgeToKubernetes.EndpointManager NuGet package (for insertion into VS and the CLI), specify the PublishProfile like this:
```
dotnet publish endpointmanager.csproj -r win-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
```

To build the Microsoft.BridgeToKubernetes.EndpointManager NuGet package, publish for all platforms and then run `dotnet pack`:
```
dotnet publish endpointmanager.csproj -r win-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
dotnet publish endpointmanager.csproj -r osx-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
dotnet publish endpointmanager.csproj -r linux-x64 -p:PublishProfile=Properties\PublishProfiles\FolderProfile.pubxml
dotnet pack endpointmanager.csproj -p:NuspecFile=EndpointManager.nuspec
```

Running/Debugging
-----------------
**Running:** Publish the `endpointmanager.csproj` project and run the `EndpointManager.exe` executable to test the your changes.
```
dotnet publish src\EndpointManager\endpointmanager.csproj -r win-x64
```

**Debugging:** In Visual Studio, right click the `endpointmanager.csproj` and choose "Set as Startup Project". In the project's `properties` > `Debug` tab change the "Launch" dropdown to "Executable" and enter the path to your local built copy of EndpointManager.exe like `C:\Mindaro\src\EndpointManager\bin\Debug\net7.0\win-x64\EndpointManager.exe`.

Testing
-------
Manual testing instructions for the Bridge to Kubernetes experience: [BridgeToKubernetes.md](../../test/manual/BridgeToKubernetes.md)

Troubleshoot
------------
< Describe the trouble shooting steps which help to easily identify the problem. It would be helpful to list common error cases. >

Release Management
------------------
TBD - we need to put new git pipelines here.

