#!/bin/bash
export LANG=C.UTF-8
set -ef

stop_b2k() {
    echo "stopping b2k debugging via control port"
    curl -X POST http://localhost:51424/api/remoting/stop/
    echo "killing npm & node"
    sudo kill -9 $(ps aux | grep '\snode\s' | awk '{print $2}')
    sleep 5
    if [ "$RUNNER_OS" == "Linux" ]; then
        echo "killing minikube tunnel"
        kill $tunnelPID
        sleep 5
    fi
}

stop_b2k_windows() {
    echo "stopping b2k debugging via control port"
    curl -X POST http://localhost:51424/api/remoting/stop/
    echo "killing npm & node"
    taskkill -im "node.exe" -f
    sleep 5
}

validate_b2k_is_running() {
    echo "evaluating curl response after b2k debugging"
    check_if_restore_pod_exists
    validate_restore_pod_status
    if [ "$RUNNER_OS" == "Linux" ]; then
        CURL_OUTPUT=$(curl -s -w "%{http_code}" $(minikube service frontend -n todo-app --url)/api/stats)
    else
        CURL_OUTPUT=$(curl -s -w "%{http_code}" $(kubectl get service frontend -n todo-app -o jsonpath="{.status.loadBalancer.ingress[0].ip}")/api/stats)
    fi
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

check_if_restore_pod_exists() {
    ## see if b2k pods are present
    RESTORE_POD_NAME=$(kubectl get pods -n todo-app -o custom-columns=NAME:.metadata.name | grep -w "restore")
    echo "Restore Pod name is:$RESTORE_POD_NAME"
    if [ -z $RESTORE_POD_NAME ]; then
        echo "B2K restore pod is not found"
        exit 1
    fi
}

validate_restore_pod_status() {
    # make sure restore pod is in running state
    RESTORE_POD_STATUS=$(kubectl get pods -n todo-app -l mindaro.io/component=lpkrestorationjob -o=jsonpath='{.items[*].status.phase}')
    echo "restore pod status is:$RESTORE_POD_STATUS"
    if [[ -z $RESTORE_POD_STATUS || $RESTORE_POD_STATUS != 'Running' ]]; then 
        echo "restore pod is not in running state"
        exit 1
    fi
}

ensure_b2k_is_disconnected() {
    echo "ensure b2k is disconnected successfully"
    ## see if b2k pods are present, future iterations check the image name
    RESTORE_POD_NAME_FOR_DISCONNECT=$(kubectl get pods -n todo-app -o custom-columns=NAME:.metadata.name | grep -P "restore")
    echo "restore pod name after disconnection:$RESTORE_POD_NAME_FOR_DISCONNECT"
    if [[ -z $RESTORE_POD_NAME_FOR_DISCONNECT ]]; then
        echo "B2K restore pod is not present after disconnection"
        exit 0
    else 
        echo "B2K restore pod is still present after disconnection"
        exit 1
    fi
}

start_b2k() {
    echo 'Starting Bridge'
    ## & for parallel execution , && for sequential execution
    ../../../src/dsc/bin/Debug/net7.0/$OS/dsc connect --service stats-api --namespace todo-app --local-port 3001 --control-port 51424 --use-kubernetes-service-environment-variables -- npm run start & b2kPID=$!
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
    dotnet publish src/dsc/dsc.csproj -c Debug -r $OS --self-contained true 
    dotnet publish src/endpointmanager/endpointmanager.csproj -c Debug -r $OS
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

check_if_exists() {
    log INFO "checking if $1 exists"
    if ! [[ -x "$(command -v $1)" ]]; then
        log INFO "Error: $1 is not installed." >&2
        result=1
    else 
        result=0
    fi
}

log() {
    local level=$1
    shift
    echo "$(date -u $now) - $level - $*"
}

start_live_test() {
    echo "Starting live testing for $OS"
    check_kubectl_present
    check_jq_processor_present

    dotnet_publish_for_b2k
    set_up_stats_api
    if [ "$RUNNER_OS" == "Linux" ]; then
        start_minikube_tunnel
    fi
    #set bridge as dev environment to run live test because some of image tags might noe be available in production
    export BRIDGE_ENVIRONMENT='dev'
    start_b2k

    validate_b2k_is_running

    if [ "$RUNNER_OS" == "Windows" ]; then
        stop_b2k_windows
    else
        stop_b2k
    fi

    #ensure_b2k_is_disconnected

    echo "live test result (true - failure, false - passed):$B2K_LIVE_TEST_FAILED"
    if [ '$B2K_LIVE_TEST_FAILED' == true ]; then
        echo "exit 1"
        exit 1
    else 
        echo "exit 0"
        exit 0
    fi
}

OS=$1
start_live_test