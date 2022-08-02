Use the GenerateServices.ps1 to generate and deploy many deployments and services.

[-c/-count] to define how many (default 150)

[-n/-namespace] to define where the namespace where to deploy them (default is current namespace from kubeconfig)


This is useful to stress test Bridge when many services are in the same namespace as the service being debugged.