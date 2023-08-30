# Managed Identity Sample

 Introduction
 ------------

 This project can be used as a sample to test managed identity and pod identity support in Bridge To Kubernetes. 
 
 This code is a web application that exposes an endpoint /mi on port 80. There are 2 calls that are made with managed identity creds:
 1. Call to upload a blob to Storage account container - This call should return unauthorized. We log and swallow the exception.
 2. Call to check if the container exists - This call should succeed.

 Dockerfile and deploy.yaml are resources to help deploy this app to an AKS cluster.
 deploy.yaml has hard coded values for the storage account and managed identity from below:
    # Sub: Mindaro Testing (c2e0f009-a61a-4578-8a6d-5715ee782648)
    # RG: testing-scenario
    # Namespace: mi-webapp
    # Storage account name: mitestsa
    # Managed identity name: mi-test

If you want to test with pod identity as well, please follow this link to setup pod identity on your cluster:
https://azure.github.io/aad-pod-identity/docs/demo/standard_walkthrough/

1. Deploy the pod identity components from Step 1 in the link. 
ex: this deploys CRD's to the cluster
```
kubectl apply -f https://raw.githubusercontent.com/Azure/aad-pod-identity/master/deploy/infra/deployment-rbac.yaml

# For AKS clusters, deploy the MIC and AKS add-on exception by running -
kubectl apply -f https://raw.githubusercontent.com/Azure/aad-pod-identity/master/deploy/infra/mic-exception.yaml 

```
2. Assign the appropriate permissions to the AKS Managed Identity: https://azure.github.io/aad-pod-identity/docs/getting-started/role-assignment/
ex: Run the roleassigment bash script
```
./samples/managed-identity/roleAssignment.sh
```

3. Deploy the mi web app to AKS cluster by running 
```
kubectl create ns mi-webapp
kubectl apply -f deploy.yaml -n mi-webapp
```

 Pre-requisites
 --------------

 1. AKS cluster with managed identity enabled.
 2. Storage account "mitestsa" in the same subscription.
 3. Container "mitestsa-container" inside the above storage account.
 4. A managed identity credential "mi-test" in the same subscription. It should have "Reader" access to the above container in the storage account.
 5. The MI should also be added to all nodes/to the node pool of the AKS cluster for managed identity token to be fetched in AKS. (only if you are setting up with managed identity, this step is not required for pod identity)

 Note: In case you want to update the above values to your own values, please update deploy.yaml and KubernetesLocalProcessConfig.yaml also.

 Build Instructions
 ------------------

 docker build -f Dockerfile -t <docker repo name>/mi-webapp .

 Testing Instructions
 --------------------

 Once the above pre-requisites are setup and you have the right values in deploy.yaml and KubernetesLocalProcessConfig.yaml,
 1. Test using Bridge To Kubernetes in both Isolated and non-isolated modes.
 NOTE: Since this is a managed identity sample, please note that testing this locally will not work.