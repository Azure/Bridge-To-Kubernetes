#!/bin/bash
# Verify if B2K is disconnected successfully

verify() {
    echo "verifying if cluster is restored to original state"
    verify_if_restore_pod_exists
    verify_if_multiple_pods_exists
    echo "multiple pod var is: $MULTIPLE_POD_EXISTS"
    if [ $MULTIPLE_POD_EXISTS == 'true' ]; then
        echo "exit 1"
        kubectl get pods -n todo-app
        exit 1
    fi 
    exit 0;

}

verify_if_restore_pod_exists() {
    echo "Verifying if restore pod exists"
    RESTORE_POD=$(kubectl get pods -n todo-app | grep "restore")
    echo "Restore Pod Name:$RESTORE_POD"
    if [ -z $RESTORE_POD ]; then
        echo "restore pod doesn't exist"
    else 
        echo "restore pod exists"
    fi
}

verify_if_multiple_pods_exists() {
    echo "Verifying if multiple pods exists"
    count=0
    MULTIPLE_POD_EXISTS=false
    while [ "$count" -le 10 ]; do
        STATS_API=$(kubectl get pods -n todo-app | grep "stats-api")
        echo "stats api pods: $STATS_API"
        echo "stats api length: ${#STATS_API}"
        if [ ! -z "$STATS_API" ] && [ "${#STATS_API}" -gt 75 ]; then 
            MULTIPLE_POD_EXISTS=true
            echo "retry count is $count"
            echo "multiple stats-api pod exists, sleeping for 5 seconds to retry"
            ((count++))
            sleep 5
        else 
            MULTIPLE_POD_EXISTS=false
            echo "multiple pods doesn't exist"
            break
        fi
    done
}

verify