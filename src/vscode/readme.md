# Bridge to Kubernetes

*Bridge to Kubernetes collects usage data and sends it to Microsoft to help improve our products and services. Read our [privacy statement](https://aka.ms/bridge-to-k8s-privacy) to learn more.*

**With Bridge to Kubernetes, the only thing you need to run and debug on your development machine is the microservice you're working on and your preferred dev tools.**

Bridge to Kubernetes extends the Kubernetes perimeter to your development computer allowing you to write, test, and debug microservice code while connected to your Kubernetes cluster with the rest of your application or services. With this workflow, there is no need for extra assets, such as a Dockerfile or Kubernetes manifests. You can simply run your code natively on your development workstation while connected to the Kubernetes cluster, allowing you to test your code changes in the context of the larger application.   

<img src="https://aka.ms/bridge-to-k8s-graphic-non-isolated" alt="Bridge to Kubernetes development model" width="680" />

## Key Features

### Simplifying Microservice Development

Eliminate the need to manually source, configure and compile external dependencies on your development computer.

### Easy Debugging

Run your usual debug profile with the added cluster configuration. You can debug your code as you normally would while taking advantage of the speed and flexibility of local debugging. 

### Developing and Testing End-to-End

Test end-to-end during development time. Select an existing service in the cluster to route to your development machine where an instance of that service is running locally. Requests initiated through the frontend of the application running in Kubernetes will route through services running in the cluster until the service you specified to redirect is called. 

## Getting Started

- [How Bridge to Kubernetes works](https://aka.ms/how-bridge-to-k8s-works)
- [How to use Bridge to Kubernetes in Visual Studio Code](https://aka.ms/bridge-to-k8s-vscode-quickstart)
- [File an issue](https://aka.ms/mindaro-issues)