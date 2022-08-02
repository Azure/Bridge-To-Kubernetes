# common assembly

 Introduction
 ------------
 This project is used with other projects and contains common functionalities used in our product and it's backend dependencies.

 Developing
------------
 < List down any pre-reqs for working on this project for a developer to be aware of. List out any changes required to point this project to use our dev resources.>

 Building
-------------
 < Describe the steps to build this project. Provide a sample docker build commands if this project is deployed as docker image. >

Running/Debugging
-------
< Describe the steps to run this project. Also list out any additional tools that would help easily test this project. >

Testing
-------
 Since this project is used with other dll's testing this process should be done by following the testing instructions mentioned by respective projects.  
 ### Automated testing:  
 `common.tests.csproj` covers the unit test cases for helper methods in this project.  
 For certain classes mentioned below for which unit test cases are intentionally not covered:  
  * `webutilities`
  * `RemoteEnvironmentUtilities`
  * `TimespanExtensionTests`
  * `OperationContextExtensions`

Troubleshoot
------------
 < Describe the trouble shooting steps which help to easily identify the problem. It would be helpful to list common error cases. >

Release Management
------------------
 < Describe what steps are configured in build and release definitions for this project which makes this project available to be consumed or used. >