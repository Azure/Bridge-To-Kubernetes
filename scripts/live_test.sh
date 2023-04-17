#!/bin/bash
set -ef

stop_b2k() {
    echo "stopping b2k debugging via control port"
    curl -X POST http://localhost:51424/api/remoting/stop/
    sleep 5
    echo "killing minikube"
    kill $tunnelPID
}

validate_b2k_is_running() {
    echo "evaluating curl response after b2k debugging"
    ## see if b2k pods are present
    RESTORE_POD=kubectl get pod -n todo-app -o json | jq '.items[] | select(.metadata.name | contains("restore"))'
    echo "Restore Pod name is:$RESTORE_POD"
    if [ -z $RESTORE_POD ]; then
    echo "B2K restore pod is not found"
    exit 1
    fi
    CURL_OUTPUT=$(curl -s -w "%{http_code}" $(minikube service frontend -n todo-app --url)/api/stats)
    echo "curl response is:$CURL_OUTPUT"
    if [[ "$CURL_OUTPUT" =~ "200" ]]; then
        echo "B2K Debugging was successful"
        B2K_LIVE_TEST_FAILED=false
    else 
        echo "B2K Debugging failed"
        B2K_LIVE_TEST_FAILED=true
    fi
    sleep 5
}

start_b2k() {
    echo 'Starting Bridge'
    ## & for parallel execution , && for sequential execution
    ../../../src/dsc/bin/Debug/net6.0/linux-x64/dsc connect --service stats-api --namespace todo-app --local-port 3001 --control-port 51424 --use-kubernetes-service-environment-variables -- npm run start & b2kPID=$!
    sleep 30
    echo "b2k process ID is $b2kPID"
}

start_minikube_tunnel() {
    echo 'Starting minikube tunnel'
    minikube tunnel  > /dev/null 2>&1 & tunnelPID=$!
    sleep 10
    echo 'verifying if minikube tunnel works'
    curl $(minikube service frontend -n todo-app --url)/api/stats
}

set_up_stats_api() {
    echo 'cd into stats-api folder'
    cd samples/todo-app/stats-api
    echo 'npm install'
    npm i
}

dotnet_publish_for_b2k() {
    echo "dotnet publishing dsc"
    dotnet publish src/dsc/dsc.csproj -c Debug -r linux-x64 --self-contained true 
    dotnet publish src/endpointmanager/endpointmanager.csproj -c Debug -r linux-x64
}

# Check JQ Processor and download if not present
check_jq_processor_present() {
    check_if_exists jq
    if [[ $result != 0 ]]; then
        install_tool jq
    fi
    jqversion=$(jq --version)
    log INFO "Locally installed JQ Processor version is $jqversion"
}

check_kubectl_present() {
    check_if_exists kubectl
    if [[ $result != 0 ]]; then
        install_tool kubectl
    fi
    kubectlversion=$(kubectl version --client=true -o json | jq ".clientVersion.gitVersion")
    log INFO "Locally installed kubectl version is $kubectlversion"
}

start_live_test() {
    echo "Starting live testing for B2K"
    check_kubectl_present
    check_jq_processor_present

    dotnet_publish_for_b2k
    set_up_stats_api
    start_minikube_tunnel
    start_b2k

    validate_b2k_is_running

    stop_b2k

    echo "live test result (true - failure, false - passed):$B2K_LIVE_TEST_FAILED"
    if [ '$B2K_LIVE_TEST_FAILED' == true ]; then
        echo "exit 1"
        exit 1
    else 
        echo "exit 0"
        exit 0
    fi
}

start_live_test
