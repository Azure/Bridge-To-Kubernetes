# Client SDK

Introduction
------------
This project contains the Client SDK that is used by the CLI and VS to interact with our services and perform shared client tasks (e.g. prep projects).

Developing
------------
The SDK project targets both DotNet Core and DotNet Framework. The minimum requirement is a text editor and the DotNet Core runtime but full VS is recommended for a better developing and debug experience.

Set `BRIDGE_ENVIRONMENT` environment variable to following values to debug/run specific environment of the tools.  
* `dev`: Targets development environment
* `staging`: Targets staging environment
* `production` or don't set any value: Targets prod environment

Building
-------------
To build the project run the following command:

`dotnet build src\client\client.csproj`

Running/Debugging
-------
**CLI Debugging:**
Since the SDK is used by the CLI, the [Running/Debugging instructions of the CLI](..\cli\readme.md) can be followed to test the SDK too.

Testing
-------
**Automated:** 
The `src\client.tests\` project covers the unit test cases for this project.  
Run `dotnet test src\client.tests\client.tests.csproj` command to execute all the unit test cases for this project. 

*Most classes are already covered but there are still some that are only partially covered, as proof of concept, before extending the whole coverage:*

    Partially covered:
    * ManagementClientImplementation.cs
    * ProgressReporter.cs
    * ConfigFileUpgrader.cs
    * ManagementClientFactory.cs

Troubleshoot
------------
< Describe the trouble shooting steps which help to easily identify the problem. It would be helpful to list common error cases. >

Release Management
------------------
 Build definition: [Mindaro-Cli-new](TBD need new git pipelines)

 Release definition: [Mindaro-Cli-new](TBD need new git pipelines)