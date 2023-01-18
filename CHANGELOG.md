# Release notes

Please note this is Bridge to Kubernetes as a tool change log. If you are using one of the extension built on top of it, also check the extension changelog to see what version of B2K they are running.
- [Visual Studio Code extension release notes](https://github.com/Azure/vscode-bridge-to-kubernetes/blob/main/CHANGELOG.md)
- [Visual Studio 2019 extension release notes](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.mindaro#whats-new)
- [Visual Studio 2022 extension release notes](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.mindaro2022#whats-new)

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
