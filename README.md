
# Bridge to Kubernetes

[![Build Status](https://devdiv.visualstudio.com/DevDiv/_apis/build/status/Azure.Bridge-To-Kubernetes?branchName=main)](https://devdiv.visualstudio.com/DevDiv/_build/latest?definitionId=17861&branchName=main)

Welcome to Bridge-To-Kubernetes! Bridge to Kubernetes extends the Kubernetes perimeter to your development computer allowing you to write, test, and debug microservice code while connected to your Kubernetes cluster with the rest of your application or services. With this workflow, there is no need for extra assets, such as a Dockerfile or Kubernetes manifests. You can simply run your code natively on your development workstation while connected to the Kubernetes cluster, allowing you to test your code changes in the context of the larger application.



## Key Features:

### Simplifying Microservice Development 
- Eliminate the need to manually source, configure and compile external dependencies on your development computer.  

### Easy Debugging 
- Run your usual debug profile with the added cluster configuration. You can debug your code as you normally would while taking advantage of the speed and flexibility of local debugging. 

### Developing and Testing End-to-End 
- Test end-to-end during development time. Select an existing service in the cluster to route to your development machine where an instance of that service is running locally. Request initiated through the frontend of the application running in Kubernetes will route between services running in the cluster until the service you specified to redirect is called. 

## Documentation
- [Overview](https://learn.microsoft.com/visualstudio/bridge/overview-bridge-to-kubernetes)
- [Visual Studio](https://learn.microsoft.com/visualstudio/bridge/bridge-to-kubernetes-vs)
- [Visual Studio Code](https://learn.microsoft.com/visualstudio/bridge/bridge-to-kubernetes-vs-code)

## CLI tool installation
- ```curl -fsSL https://raw.githubusercontent.com/Azure/Bridge-To-Kubernetes/main/scripts/install.sh | bash```
- Supports Linux, Darwin, Windows - use WSL (installation link [here](https://learn.microsoft.com/en-us/windows/wsl/install)) or Git Bash (installation link [here](https://git-scm.com/))

## How to use the CLI
- run the following command ``` dsc connect --service <service-name> --local-port <port-number> --namespace <namespace> --use-kubernetes-service-environment-variables ```
- ```example is dsc connect --service stats-api --local-port 3001 --namespace todo-app```
- for help  ``` dsc --help```
- for version ```dsc --version```

## Microsoft Open Source Code of Conduct
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/)
 
## Trademarks
This project may contain trademarks or logos for projects, products, or services. Authorized use of Microsoft trademarks or logos is subject to and must follow [Microsoft’s Trademark & Brand Guidelines] (https://www.microsoft.com/en-us/legal/intellectualproperty/trademarks). Use of Microsoft trademarks or logos in modified versions of this project must not cause confusion or imply Microsoft sponsorship. Any use of third-party trademarks or logos are subject to those third-party’s policies.
 
## Security Reporting Guidance
Checkout the SECURITY.md file in this repo for details.

## Data Collection
The software may collect information about you and your use of the software and send it to Microsoft. Microsoft may use this information to provide services and improve our products and services. You may turn off the telemetry as described in the repository. There are also some features in the software that may enable you and Microsoft to collect data from users of your applications. If you use these features, you must comply with applicable law, including providing appropriate notices to users of your applications together with a copy of Microsoft’s privacy statement. Our privacy statement is located at https://go.microsoft.com/fwlink/?LinkID=824704. You can learn more about data collection and use in the help documentation and our privacy statement. Your use of the software operates as your consent to these practices.

## Support

Bridge to Kubernetes is an open source project that is not covered by the [Microsoft Azure support policy](https://docs.microsoft.com/en-US/troubleshoot/azure/cloud-services/support-linux-open-source-technology). [Please search open issues here](https://github.com/Azure/Bridge-To-Kubernetes/issues), and if your issue isn't already represented [please open a new one](https://github.com/Azure/Bridge-To-Kubernetes/issues/new/choose). The project maintainers will respond to the best of their abilities and triage the most urgent bugs.


