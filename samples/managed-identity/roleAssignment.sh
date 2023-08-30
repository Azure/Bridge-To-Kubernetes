export SUBSCRIPTION_ID="c2e0f009-a61a-4578-8a6d-5715ee782648"
export RESOURCE_GROUP="testing-scenario"
export CLUSTER_NAME="testing-scenarios"

curl -s https://raw.githubusercontent.com/Azure/aad-pod-identity/master/hack/role-assignment.sh | bash