# Release notes

This is the change log of Bridge to Kubernetes which includes binaries, container images, and CLI.

If you are using one of the IDE extensions for Bridge to Kubernetes, check the respective extension's changelog to see what version that extension is using.
- [Visual Studio Code extension release notes](https://github.com/Azure/vscode-bridge-to-kubernetes/blob/main/CHANGELOG.md)
- [Visual Studio 2019 extension release notes](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.mindaro#whats-new)
- [Visual Studio 2022 extension release notes](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.mindaro2022#whats-new)

## [1.0.20230327.2]
- Address [Remove BouncyCastle Nuget package since .NET 6 can handle this internally](https://github.com/Azure/Bridge-To-Kubernetes/pull/183)
- Address [Remove minimatch and replace it with internal .NET package](https://github.com/Azure/Bridge-To-Kubernetes/pull/184)
- Address [Restore job failure fix ](https://github.com/Azure/Bridge-To-Kubernetes/pull/203)
- Address [Introduce new telemetry data for FailureReason excluding PII data](https://github.com/Azure/Bridge-To-Kubernetes/pull/208)
- Address [Disable probes feature](https://github.com/Azure/Bridge-To-Kubernetes/pull/164)
- Address [Support nodeport services for bridge](https://github.com/Azure/Bridge-To-Kubernetes/pull/206)
- Other minor dependant bot fixes

## [1.0.20230310.3]
- Address [issue around named port environment variables not being created](https://github.com/Azure/Bridge-To-Kubernetes/issues/165) 
- Address [migrating from netwonsoft to stj](https://github.com/Azure/Bridge-To-Kubernetes/issues/134)
- Address [fix to AddhostFileEntry tuple](https://github.com/Azure/Bridge-To-Kubernetes/issues/135)
- Address [JSon serialization issue](https://github.com/Azure/Bridge-To-Kubernetes/issues/55)
- Address [headless service not getting forwarded all the time](https://github.com/Azure/Bridge-To-Kubernetes/issues/167)
- Other minor bug fixes.

## [1.0.20221219.2]
- Dotnet6 upgrade - minimum required dotnet version is 6.0.11 and downloads it when latest extension is used. **NOTE:** Going back to previous version of extension requires manual action by user to download the dotnet 3.1.6 and replace it in downloaded location. Extension would not download because 6.0.11 is higher than 3.1.6.
- Manage Identity fixes
- Fixes when using useKubernetesServiceEnvironmentVariables feature
 
## [1.0.120220906]

- Updating the code samples for CLI.

## [1.0.120220819]

- Initial Release from internal repo
- Add github workflows for code ql, release binaries
- Add kubectl library download step
